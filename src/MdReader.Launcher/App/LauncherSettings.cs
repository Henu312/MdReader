namespace MdReader.App;

/// <summary>启动器配置：browser.txt 决定用哪个浏览器，extension-id.txt 决定扩展 ID。</summary>
internal sealed record LauncherSettings(string? Browser, string? ExtensionId)
{
    public static LauncherSettings Load() => LoadFrom(AppContext.BaseDirectory);

    public static LauncherSettings LoadFrom(string directory)
    {
        var browser = ConfigParser.ParseBrowserChoice(ReadText(Path.Combine(directory, "browser.txt")));
        var extensionId = ConfigParser.ParseExtensionId(ReadLines(Path.Combine(directory, "extension-id.txt")), browser);
        return new LauncherSettings(browser, extensionId);
    }

    private static string? ReadText(string path) => File.Exists(path) ? File.ReadAllText(path) : null;

    private static IEnumerable<string> ReadLines(string path) => File.Exists(path) ? File.ReadAllLines(path) : Array.Empty<string>();
}

/// <summary>配置文件的解析规则，保持无副作用以便自检覆盖。</summary>
internal static class ConfigParser
{
    /// <summary>只认 chrome / brave，其他值（包括 auto 和空值）都表示“自动探测”。</summary>
    public static string? ParseBrowserChoice(string? content)
    {
        var value = content?.Trim().ToLowerInvariant();
        return value is "chrome" or "brave" ? value : null;
    }

    /// <summary>
    /// 支持 <c>brave=ID</c>、<c>chrome=ID</c>、<c>auto=ID</c> 三种写法，
    /// 也兼容直接写一行裸 ID（按最后兜底处理）。
    /// </summary>
    public static string? ParseExtensionId(IEnumerable<string> lines, string? browser)
    {
        var byBrowser = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string? bare = null;

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            var separator = line.IndexOf('=');
            if (separator > 0)
            {
                var key = line[..separator].Trim().ToLowerInvariant();
                var value = line[(separator + 1)..].Trim();
                if (IsExtensionId(value))
                {
                    byBrowser[key] = value;
                }
            }
            else if (bare is null && IsExtensionId(line))
            {
                bare = line;
            }
        }

        if (browser is not null && byBrowser.TryGetValue(browser, out var selected))
        {
            return selected;
        }

        if (byBrowser.TryGetValue("auto", out var automatic))
        {
            return automatic;
        }

        if (bare is not null)
        {
            return bare;
        }

        // browser.txt 写 auto 时这里拿到的 browser 是 null；
        // 只配置了一个浏览器的 ID 就直接用它，否则退回后备页。
        return byBrowser.Count == 1 ? byBrowser.Values.First() : null;
    }

    /// <summary>Chromium 扩展 ID：32 位，字符范围为 a-p。</summary>
    public static bool IsExtensionId(string value) => value.Length == 32 && value.All(c => c is >= 'a' and <= 'p');
}
