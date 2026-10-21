using System.Net.Http;
using System.Text;
using MdReader.App;
using MdReader.Server;

namespace MdReader;

/// <summary>
/// 无界面自检：<c>MdReader.exe --selftest</c>。
/// 第一部分检查纯逻辑（参数解析、配置解析、路径判断）；
/// 第二部分在本机起一次真实 HTTP 服务，跑一遍四个路由。
/// 如果当前环境不允许绑定 127.0.0.1，第二部分标记为 SKIP，不算失败。
/// </summary>
internal static class SelfTest
{
    private static int _failures;

    public static async Task<int> RunAsync()
    {
        // 输出固定为 UTF-8，重定向到文件或管道时不会变成系统代码页。
        try
        {
            Console.OutputEncoding = Encoding.UTF8;
        }
        catch
        {
            // 没有控制台时忽略。
        }

        _failures = 0;
        CheckLogic();
        await CheckHttpAsync().ConfigureAwait(false);

        Console.WriteLine(_failures == 0 ? "自检通过" : $"自检失败 {_failures} 项");
        return _failures == 0 ? 0 : 1;
    }

    private static void Check(string name, bool passed, string detail = "")
    {
        Console.WriteLine($"{(passed ? "PASS" : "FAIL")}  {name}{(detail.Length > 0 ? "  (" + detail + ")" : "")}");
        if (!passed)
        {
            _failures++;
        }
    }

    private static void Skip(string name, string reason) => Console.WriteLine($"SKIP  {name}  ({reason})");

    private static void CheckLogic()
    {
        var workspace = Path.Combine(Path.GetTempPath(), $"mdreader-selftest-{Guid.NewGuid():N}");
        Directory.CreateDirectory(workspace);
        try
        {
            var markdown = Path.Combine(workspace, "示例.md");
            var plainText = Path.Combine(workspace, "说明.txt");
            File.WriteAllText(markdown, "# 标题");
            File.WriteAllText(plainText, "not markdown");

            Check("解析 .md 参数", MarkdownFile.TryResolve(markdown, out var resolved) && resolved == Path.GetFullPath(markdown));
            Check("去掉多余引号", MarkdownFile.TryResolve($"\"{markdown}\"", out _));
            Check("拒绝非 Markdown", !MarkdownFile.TryResolve(plainText, out _));
            Check("拒绝不存在的文件", !MarkdownFile.TryResolve(Path.Combine(workspace, "缺失.md"), out _));

            Check("目录越界判断",
                !MarkdownFile.IsInside(@"C:\Windows\win.ini", workspace) &&
                MarkdownFile.IsInside(Path.Combine(workspace, "a.png"), workspace));
            Check("MIME 映射",
                MarkdownFile.MimeType(".PNG") == "image/png" &&
                MarkdownFile.MimeType(".unknown") == "application/octet-stream");

            Check("browser.txt 只认 chrome/brave",
                ConfigParser.ParseBrowserChoice(" Brave\r\n") == "brave" &&
                ConfigParser.ParseBrowserChoice("auto") is null);
            Check("扩展 ID 校验",
                ConfigParser.IsExtensionId(new string('a', 32)) &&
                !ConfigParser.IsExtensionId(new string('q', 32)));
            Check("auto 时单条浏览器 ID 可用",
                ConfigParser.ParseExtensionId(new[] { $"brave={new string('e', 32)}" }, null) == new string('e', 32));            Check("扩展 ID 按浏览器选择",
                ConfigParser.ParseExtensionId(new[] { $"chrome={new string('b', 32)}" }, "chrome") == new string('b', 32));
            Check("扩展 ID 回落到 auto",
                ConfigParser.ParseExtensionId(new[] { $"auto={new string('c', 32)}" }, "brave") == new string('c', 32));

            File.WriteAllText(Path.Combine(workspace, "browser.txt"), "chrome\n");
            File.WriteAllText(Path.Combine(workspace, "extension-id.txt"), "# 注释行\nchrome=" + new string('d', 32) + "\n");
            var settings = LauncherSettings.LoadFrom(workspace);
            Check("配置文件整体加载", settings.Browser == "chrome" && settings.ExtensionId == new string('d', 32));
        }
        finally
        {
            TryDelete(workspace);
        }
    }

    private static async Task CheckHttpAsync()
    {
        var workspace = Path.Combine(Path.GetTempPath(), $"mdreader-selftest-http-{Guid.NewGuid():N}");
        Directory.CreateDirectory(workspace);
        var markdown = Path.Combine(workspace, "自检文档.md");
        var picture = Path.Combine(workspace, "pic.png");
        var secretName = $"mdreader-secret-{Guid.NewGuid():N}.txt";
        var secret = Path.Combine(Path.GetTempPath(), secretName);

        File.WriteAllText(markdown, "# 你好\n\n正文第一段");
        File.WriteAllBytes(picture, new byte[] { 1, 2, 3, 4 });
        File.WriteAllText(secret, "不该被读到");

        DocumentServer server;
        try
        {
            server = DocumentServer.Start(new DocumentStore());
        }
        catch (Exception error)
        {
            Skip("HTTP 路由", "当前环境无法绑定 127.0.0.1：" + error.Message);
            TryDelete(workspace);
            TryDeleteFile(secret);
            return;
        }

        try
        {
            var viewerUrl = server.CreateDocumentUrl(markdown, new LauncherSettings(Browser: null, ExtensionId: null));
            var token = new Uri(viewerUrl).Query.TrimStart('?')
                .Split('&')
                .Select(part => part.Split('=', 2))
                .First(pair => pair[0] == "doc")[1];

            _ = Task.Run(() => server.RunAsync(TimeSpan.FromSeconds(15)));
            await Task.Delay(150).ConfigureAwait(false);

            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };

            var content = await http.GetAsync($"{server.Origin}/content?doc={token}").ConfigureAwait(false);
            Check("GET /content 返回正文",
                content.IsSuccessStatusCode && (await content.Content.ReadAsStringAsync().ConfigureAwait(false)).Contains("正文第一段"));

            var noToken = await http.GetAsync($"{server.Origin}/content").ConfigureAwait(false);
            Check("GET /content 不带令牌 → 404", (int)noToken.StatusCode == 404);

            var badToken = await http.GetAsync($"{server.Origin}/content?doc=deadbeef").ConfigureAwait(false);
            Check("GET /content 错误令牌 → 404", (int)badToken.StatusCode == 404);

            var asset = await http.GetAsync($"{server.Origin}/asset?doc={token}&path=pic.png").ConfigureAwait(false);
            Check("GET /asset 同目录图片",
                asset.IsSuccessStatusCode && (await asset.Content.ReadAsByteArrayAsync().ConfigureAwait(false)).Length == 4);

            var escape = await http.GetAsync($"{server.Origin}/asset?doc={token}&path=../{secretName}").ConfigureAwait(false);
            Check("GET /asset 目录逃逸 → 404", (int)escape.StatusCode == 404);

            var save = await http.PostAsync($"{server.Origin}/save?doc={token}", new StringContent("# 改过了")).ConfigureAwait(false);
            Check("POST /save 写回原文件", save.IsSuccessStatusCode && File.ReadAllText(markdown) == "# 改过了");

            var saveGet = await http.GetAsync($"{server.Origin}/save?doc={token}").ConfigureAwait(false);
            Check("GET /save → 405", (int)saveGet.StatusCode == 405);

            var fallback = await http.GetAsync(viewerUrl).ConfigureAwait(false);
            Check("GET / 后备页替换标题",
                fallback.IsSuccessStatusCode && (await fallback.Content.ReadAsStringAsync().ConfigureAwait(false)).Contains("自检文档.md"));

            var rootWithoutToken = await http.GetAsync($"{server.Origin}/").ConfigureAwait(false);
            Check("GET / 不带令牌 → 404", (int)rootWithoutToken.StatusCode == 404);

            var options = await http.SendAsync(new HttpRequestMessage(HttpMethod.Options, $"{server.Origin}/content")).ConfigureAwait(false);
            Check("OPTIONS 预检 → 204", (int)options.StatusCode == 204);
        }
        finally
        {
            server.Dispose();
            TryDelete(workspace);
            TryDeleteFile(secret);
        }
    }

    private static void TryDelete(string directory)
    {
        try
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
        catch
        {
            // 清理失败不影响结论。
        }
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // 清理失败不影响结论。
        }
    }
}
