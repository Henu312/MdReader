namespace MdReader.App;

/// <summary>与 Markdown 文件路径有关的纯函数：参数解析、目录越界判断、资源 MIME。</summary>
internal static class MarkdownFile
{
    private static readonly string[] Extensions = { ".md", ".markdown" };

    /// <summary>把命令行参数（或管道传来的路径）解析成绝对路径，只接受真实存在的 .md/.markdown。</summary>
    public static bool TryResolve(string? argument, out string path)
    {
        path = string.Empty;
        if (string.IsNullOrWhiteSpace(argument))
        {
            return false;
        }

        var candidate = argument.Trim().Trim('"');
        if (!File.Exists(candidate))
        {
            return false;
        }

        if (!Extensions.Contains(Path.GetExtension(candidate), StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }

        path = Path.GetFullPath(candidate);
        return true;
    }

    /// <summary>判断 <paramref name="path"/> 是否严格位于 <paramref name="directory"/> 之内。</summary>
    public static bool IsInside(string path, string directory)
    {
        var root = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return path.StartsWith(root, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>本地资源接口用到的 MIME 映射，未知类型按二进制流处理。</summary>
    public static string MimeType(string extension) => extension.ToLowerInvariant() switch
    {
        ".png" => "image/png",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".gif" => "image/gif",
        ".webp" => "image/webp",
        ".svg" => "image/svg+xml",
        ".css" => "text/css",
        ".js" => "text/javascript; charset=utf-8",
        ".html" or ".htm" => "text/html; charset=utf-8",
        _ => "application/octet-stream"
    };
}
