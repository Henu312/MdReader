namespace MdReader.App;

/// <summary>唯一的用户提示出口，方便以后换成日志或托盘气泡。</summary>
internal static class Ui
{
    public static void ShowHint(string message) => MessageBox.Show(
        message,
        "MdReader",
        MessageBoxButtons.OK,
        MessageBoxIcon.Information);
}
