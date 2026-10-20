using MdReader.App;
using MdReader.Server;

namespace MdReader;

/// <summary>
/// 进程入口：校验参数 → 单实例判定 → 启动本地服务 → 打开浏览器。
/// 同一个进程内后续打开的文档通过命名管道转发进来，不会重复启动进程。
/// </summary>
internal static class Program
{
    // 启动器最长存活时间，到点后进程退出，释放端口。
    private static readonly TimeSpan Lifetime = TimeSpan.FromHours(2);

    [STAThread]
    private static async Task Main(string[] args)
    {
        if (args.Length == 1 && args[0].Equals("--selftest", StringComparison.OrdinalIgnoreCase))
        {
            Environment.ExitCode = await SelfTest.RunAsync();
            return;
        }

        if (args.Length == 0 || !MarkdownFile.TryResolve(args[0], out var markdownPath))
        {
            Ui.ShowHint("请将 .md 文件关联到本程序后再打开。\n\n也可以把 Markdown 文件拖到 MdReader.exe 上测试。");
            return;
        }

        using var instance = new SingleInstance();
        if (!instance.IsOwner)
        {
            if (!await instance.ForwardToRunningAsync(markdownPath))
            {
                Ui.ShowHint("已有 MdReader 正在运行，但无法发送文件路径。请稍后重试。");
            }
            return;
        }

        var documents = new DocumentStore();
        DocumentServer server;
        try
        {
            server = DocumentServer.Start(documents);
        }
        catch (Exception error)
        {
            Ui.ShowHint($"无法启动本地预览服务：{error.Message}");
            return;
        }

        using (server)
        {
            instance.Listen(path => OpenDocument(path, documents, server));
            OpenDocument(markdownPath, documents, server);
            await server.RunAsync(Lifetime);
        }
    }

    private static void OpenDocument(string markdownPath, DocumentStore documents, DocumentServer server)
    {
        // 每次打开都重新读取配置，修改 browser.txt / extension-id.txt 后不需要重启进程。
        var settings = LauncherSettings.Load();
        BrowserLauncher.Open(server.CreateDocumentUrl(markdownPath, settings), settings.Browser);
    }
}
