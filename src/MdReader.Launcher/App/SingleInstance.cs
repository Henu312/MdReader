using System.IO.Pipes;
using System.Text;

namespace MdReader.App;

/// <summary>
/// 单实例守卫：命名互斥体保证只有一个进程，命名管道把后续打开的文件路径送进已有进程。
/// </summary>
internal sealed class SingleInstance : IDisposable
{
    private const string MutexName = @"Local\MdReader.SingleInstance";
    private const string PipeName = "MdReader.OpenFile";

    private readonly Mutex _mutex;

    public SingleInstance()
    {
        _mutex = new Mutex(initiallyOwned: true, MutexName, out var createdNew);
        IsOwner = createdNew;
    }

    public bool IsOwner { get; }

    /// <summary>主实例在后台循环接收其他进程发来的文件路径。</summary>
    public void Listen(Action<string> handler)
    {
        if (!IsOwner)
        {
            throw new InvalidOperationException("只有主实例可以接收文件转发。");
        }

        _ = Task.Run(() => ListenLoopAsync(handler));
    }

    /// <summary>把路径发给已经在运行的实例；连不上说明对方正在退出，由调用方提示用户重试。</summary>
    public async Task<bool> ForwardToRunningAsync(string markdownPath, int timeoutMilliseconds = 1500)
    {
        try
        {
            await using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out, PipeOptions.Asynchronous);
            await client.ConnectAsync(timeoutMilliseconds);
            await using var writer = new StreamWriter(client, new UTF8Encoding(false)) { AutoFlush = true };
            await writer.WriteLineAsync(markdownPath);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static async Task ListenLoopAsync(Action<string> handler)
    {
        while (true)
        {
            try
            {
                await using var server = new NamedPipeServerStream(
                    PipeName, PipeDirection.In, maxNumberOfServerInstances: 1,
                    PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
                await server.WaitForConnectionAsync();
                using var reader = new StreamReader(server, Encoding.UTF8);
                var forwarded = await reader.ReadLineAsync();
                if (MarkdownFile.TryResolve(forwarded, out var markdownPath))
                {
                    handler(markdownPath);
                }
            }
            catch
            {
                // 管道被占用或对端异常断开时稍等再重建，避免忙等。
                await Task.Delay(200);
            }
        }
    }

    public void Dispose()
    {
        if (IsOwner)
        {
            try
            {
                _mutex.ReleaseMutex();
            }
            catch (ApplicationException)
            {
                // 已释放或无所有权，忽略。
            }
        }

        _mutex.Dispose();
    }
}
