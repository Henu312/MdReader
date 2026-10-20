using System.Collections.Concurrent;

namespace MdReader.Server;

/// <summary>
/// 文档令牌表：每打开一个文件生成一个随机 token，扩展只拿到 token，
/// 服务端据此定位真实路径，避免把本地绝对路径暴露给页面。
/// </summary>
internal sealed class DocumentStore
{
    private readonly ConcurrentDictionary<string, string> _paths = new(StringComparer.Ordinal);

    public string Register(string markdownPath)
    {
        var token = Guid.NewGuid().ToString("N");
        _paths[token] = markdownPath;
        return token;
    }

    public bool TryResolve(string? token, out string markdownPath)
    {
        markdownPath = string.Empty;
        if (string.IsNullOrWhiteSpace(token) || !_paths.TryGetValue(token, out var found))
        {
            return false;
        }

        markdownPath = found;
        return true;
    }
}
