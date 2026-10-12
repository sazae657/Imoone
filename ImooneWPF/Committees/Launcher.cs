using ImogeIndex;
using ImogeIndex.Common;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;

namespace ImooneWPF.Committees;

/// <summary>
/// ﾌﾟﾛｾｽ開いた結果
/// </summary>
internal enum LaunchStatus
{
    Success,
    Denied,
    FileNotFound,
    Failed
}

/// <summary>
/// ﾌﾟﾛｾｽ開いた結果
/// </summary>
/// <param name="Status"></param>
/// <param name="Exception"></param>
internal sealed record FileLaunchResult(LaunchStatus Status, Exception? Exception = null);

/// <summary>
/// 外部ﾌﾟﾛｾｽを開くﾏﾝ
/// </summary>
internal sealed class Launcher
{
    readonly HashSet<Regex> allowIE = new();
    readonly HashSet<string> denyIE = new(StringComparer.OrdinalIgnoreCase);

    public Launcher()
    {
        allowIE.UnionWith(SystemParams.ImageExtensions.Select(extension => $"\\.{extension}\\Z".AsRegexIgnoreCase()));
        denyIE.UnionWith(SystemParams.DisallowExtensions);
    }

    /// <summary>
    /// 開く
    /// </summary>
    /// <param name="path"></param>
    /// <returns></returns>
    public FileLaunchResult Open(string path)
    {
        if (!IsSafeExecutablePath(path))
        {
            return new FileLaunchResult(LaunchStatus.Denied);
        }
        if (!File.Exists(path))
        {
            return new FileLaunchResult(LaunchStatus.FileNotFound);
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true
            });
            return new FileLaunchResult(LaunchStatus.Success);
        }
        catch (Exception ex)
        {
            return new FileLaunchResult(LaunchStatus.Failed, ex);
        }
    }

    /// <summary>
    /// pathを選択状態でExplorerを開く
    /// </summary>
    /// <param name="path"></param>
    /// <returns></returns>
    public FileLaunchResult ShowInExplorer(string path)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                UseShellExecute = true,
                Arguments = "/select,\"" + path + "\""
            });
            return new FileLaunchResult(LaunchStatus.Success);
        }
        catch (Exception ex)
        {
            return new FileLaunchResult(LaunchStatus.Failed, ex);
        }
    }

    /// <summary>
    /// それ開いて良いﾊﾟｽ？
    /// </summary>
    /// <param name="path"></param>
    /// <returns></returns>
    private bool IsSafeExecutablePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }
        var extension = Path.GetExtension(path);
        if (string.IsNullOrEmpty(extension) || denyIE.Contains(extension))
        {
            return false;
        }
        if (allowIE.Any(s => s.IsMatch(extension)))
        {
            return true;
        }
        try
        {
            return Profile.Active != null && Profile.Active.WasTargetImage(path);
        }
        catch
        {
            return false;
        }
    }
}
