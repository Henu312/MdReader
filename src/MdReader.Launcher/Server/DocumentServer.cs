using System.Net;
using System.Net.Sockets;
using System.Text;
using MdReader.App;

namespace MdReader.Server;

/// <summary>
/// 本地 HTTP 服务：只监听 127.0.0.1 的随机端口，向扩展提供正文、相对路径资源和写回接口。
/// 所有接口都要求 doc 令牌，未携带或令牌失效一律 404。
/// </summary>
internal sealed class DocumentServer : IDisposable
{
    private const long MaxSaveBytes = 64L * 1024 * 1024;

    private readonly HttpListener _listener = new();
    private readonly DocumentStore _documents;
    private readonly string _staticDirectory;

    private DocumentServer(int port, DocumentStore documents)
    {
        _documents = documents;
        _staticDirectory = AppContext.BaseDirectory;
        Port = port;
        _listener.Prefixes.Add($"http://127.0.0.1:{port}/");
    }

    public int Port { get; }

    public string Origin => $"http://127.0.0.1:{Port}";

    public static DocumentServer Start(DocumentStore documents)
    {
        var server = new DocumentServer(GetFreePort(), documents);
        server._listener.Start();
        return server;
    }

    /// <summary>
    /// 生成交给浏览器的地址：配置了扩展 ID 就打开扩展的 viewer.html，
    /// 否则退回内置后备阅读页（此时 doc 令牌会带在页面地址上）。
    /// </summary>
    public string CreateDocumentUrl(string markdownPath, LauncherSettings settings)
    {
        var token = Uri.EscapeDataString(_documents.Register(markdownPath));
        var name = Uri.EscapeDataString(Path.GetFileName(markdownPath));

        if (settings.ExtensionId is { } extensionId)
        {
            var source = Uri.EscapeDataString($"{Origin}/content?doc={token}");
            var assetBase = Uri.EscapeDataString($"{Origin}/asset");
            return $"chrome-extension://{extensionId}/viewer.html?source={source}&assetBase={assetBase}&name={name}";
        }

        return $"{Origin}/?doc={token}&name={name}";
    }

    /// <summary>顺序处理请求，到期后返回，由调用方释放监听器。</summary>
    public async Task RunAsync(TimeSpan lifetime)
    {
        var deadline = DateTime.UtcNow + lifetime;
        while (DateTime.UtcNow < deadline)
        {
            var context = await _listener.GetContextAsync();
            await HandleAsync(context);
        }
    }

    public void Dispose()
    {
        try
        {
            _listener.Stop();
        }
        catch
        {
            // 已经停止时忽略。
        }

        _listener.Close();
    }

    private async Task HandleAsync(HttpListenerContext context)
    {
        var request = context.Request;
        var response = context.Response;

        try
        {
            ApplyCors(response);
            if (IsMethod(request, "OPTIONS"))
            {
                response.StatusCode = 204;
                return;
            }

            _documents.TryResolve(request.QueryString["doc"], out var markdownPath);
            switch (request.Url?.AbsolutePath ?? "/")
            {
                case "/":
                case "/index.html":
                    await ServeFallbackAsync(response, markdownPath);
                    break;
                case "/content" when markdownPath.Length > 0:
                    await ServeContentAsync(response, markdownPath);
                    break;
                case "/save" when markdownPath.Length > 0:
                    await ServeSaveAsync(request, response, markdownPath);
                    break;
                case "/asset" when markdownPath.Length > 0:
                    await ServeAssetAsync(request, response, markdownPath);
                    break;
                default:
                    response.StatusCode = 404;
                    break;
            }
        }
        catch
        {
            TryFail(response);
        }
        finally
        {
            response.Close();
        }
    }

    private async Task ServeFallbackAsync(HttpListenerResponse response, string markdownPath)
    {
        if (markdownPath.Length == 0)
        {
            response.StatusCode = 404;
            return;
        }

        var template = Path.Combine(_staticDirectory, "viewer.html");
        if (!File.Exists(template))
        {
            response.StatusCode = 404;
            return;
        }

        var title = WebUtility.HtmlEncode(Path.GetFileName(markdownPath));
        var html = (await File.ReadAllTextAsync(template)).Replace("__TITLE__", title);
        await WriteAsync(response, html, "text/html; charset=utf-8");
    }

    private static async Task ServeContentAsync(HttpListenerResponse response, string markdownPath)
    {
        if (!File.Exists(markdownPath))
        {
            response.StatusCode = 404;
            return;
        }

        await WriteAsync(response, await File.ReadAllTextAsync(markdownPath), "text/plain; charset=utf-8");
    }

    private static async Task ServeSaveAsync(HttpListenerRequest request, HttpListenerResponse response, string markdownPath)
    {
        if (!IsMethod(request, "POST"))
        {
            response.StatusCode = 405;
            return;
        }

        if (request.ContentLength64 > MaxSaveBytes)
        {
            response.StatusCode = 413;
            return;
        }

        string text;
        using (var reader = new StreamReader(request.InputStream, new UTF8Encoding(false), detectEncodingFromByteOrderMarks: true))
        {
            text = await reader.ReadToEndAsync();
        }

        if (text.Length > MaxSaveBytes)
        {
            response.StatusCode = 413;
            return;
        }

        await WriteTextAtomicallyAsync(markdownPath, text);
        await WriteAsync(response, "{\"saved\":true}", "application/json; charset=utf-8");
    }

    /// <summary>先写临时文件再整体替换，避免写一半时留下残缺的 Markdown。</summary>
    private static async Task WriteTextAtomicallyAsync(string markdownPath, string text)
    {
        var temporary = $"{markdownPath}.mdreader-{Guid.NewGuid():N}.tmp";
        try
        {
            await File.WriteAllTextAsync(temporary, text, new UTF8Encoding(false));
            File.Move(temporary, markdownPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
    }

    private static async Task ServeAssetAsync(HttpListenerRequest request, HttpListenerResponse response, string markdownPath)
    {
        var baseDirectory = Path.GetDirectoryName(markdownPath);
        if (string.IsNullOrEmpty(baseDirectory))
        {
            response.StatusCode = 404;
            return;
        }

        var relative = request.QueryString["path"] ?? string.Empty;
        var full = Path.GetFullPath(Path.Combine(baseDirectory, relative));
        if (!MarkdownFile.IsInside(full, baseDirectory) || !File.Exists(full))
        {
            response.StatusCode = 404;
            return;
        }

        await WriteAsync(response, await File.ReadAllBytesAsync(full), MarkdownFile.MimeType(Path.GetExtension(full)));
    }

    private static async Task WriteAsync(HttpListenerResponse response, string text, string contentType)
        => await WriteAsync(response, Encoding.UTF8.GetBytes(text), contentType);

    private static async Task WriteAsync(HttpListenerResponse response, byte[] bytes, string contentType)
    {
        ApplyCors(response);
        response.ContentType = contentType;
        response.ContentEncoding = Encoding.UTF8;
        response.ContentLength64 = bytes.Length;
        await response.OutputStream.WriteAsync(bytes);
    }

    private static void ApplyCors(HttpListenerResponse response)
    {
        response.Headers["Access-Control-Allow-Origin"] = "*";
        response.Headers["Access-Control-Allow-Methods"] = "GET,POST,OPTIONS";
        response.Headers["Access-Control-Allow-Headers"] = "Content-Type";
    }

    private static void TryFail(HttpListenerResponse response)
    {
        try
        {
            response.StatusCode = 500;
        }
        catch
        {
            // 响应已经开始写出，无法再改状态码。
        }
    }

    private static bool IsMethod(HttpListenerRequest request, string method)
        => request.HttpMethod.Equals(method, StringComparison.OrdinalIgnoreCase);

    private static int GetFreePort()
    {
        var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        var port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        return port;
    }
}
