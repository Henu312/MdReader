using System.Diagnostics;

namespace MdReader.App;

/// <summary>按 browser.txt 的选择定位 Chromium 浏览器并打开目标地址。</summary>
internal static class BrowserLauncher
{
    public static void Open(string url, string? browser)
    {
        var executable = Find(browser);
        var startInfo = executable is null
            ? new ProcessStartInfo { FileName = url, UseShellExecute = true }
            : new ProcessStartInfo { FileName = executable, Arguments = url, UseShellExecute = true };
        Process.Start(startInfo);
    }

    /// <summary>找不到浏览器时返回 null，由调用方改用系统默认程序打开 URL。</summary>
    private static string? Find(string? browser)
    {
        var brave = Executables("BraveSoftware", "Brave-Browser", "brave.exe");
        var chrome = Executables("Google", "Chrome", "chrome.exe");

        var candidates = browser switch
        {
            "chrome" => chrome,
            "brave" => brave,
            _ => brave.Concat(chrome).ToArray()
        };

        return candidates.FirstOrDefault(File.Exists);
    }

    /// <summary>同时查找“程序目录”和“当前用户目录”两种安装位置。</summary>
    private static string[] Executables(string vendor, string product, string executable)
    {
        var relative = Path.Combine(vendor, product, "Application", executable);
        return new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), relative),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), relative)
        };
    }
}
