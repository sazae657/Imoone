using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

namespace ImogeIndex;

/// <summary>
/// ｸﾗｯｼｭﾚﾎﾟーﾄの書き出し(UIﾌﾚーﾑﾜーｸ非依存部分)
/// 未処理例外のﾌｯｸ自体は各ﾌﾛﾝﾄｴﾝﾄﾞのCrashDictatorでやる
/// </summary>
public static class CrashReporter
{
    /// <summary>
    /// ｸﾗｯｼｭﾚﾎﾟーﾄ置き場
    /// </summary>
    const string CrashReportDir = "Essen+Fressen";

    /// <summary>
    /// 残しておくｸﾗｯｼｭﾚﾎﾟーﾄの数
    /// </summary>
    const int ReportGenerationCount = 20;

    /// <summary>
    /// この期間内に閾値を超えてUIｽﾚｯﾄﾞで例外が出たら続行を諦める
    /// </summary>
    static readonly TimeSpan BurstWindow = TimeSpan.FromSeconds(10);

    /// <summary>
    /// 続行を諦める例外回数
    /// </summary>
    const int BurstThreshold = 5;

    static readonly object syncRoot = new();
    static readonly Queue<DateTime> recentFaults = new();

    /// <summary>
    /// ｱﾌﾟﾘ名(ﾚﾎﾟーﾄのﾌｧｲﾙ名に使う)
    /// </summary>
    public static string AppName { get; set; } = nameof(ImogeIndex);

    /// <summary>
    /// 短時間に例外が連発しているか
    /// </summary>
    public static bool IsBurstError()
    {
        var now = DateTime.UtcNow;
        lock (syncRoot) {
            recentFaults.Enqueue(now);
            while (recentFaults.Count > 0 && now - recentFaults.Peek() > BurstWindow) {
                recentFaults.Dequeue();
            }
            return recentFaults.Count >= BurstThreshold;
        }
    }

    /// <summary>
    /// 例外のｸﾗｯｼｭﾚﾎﾟーﾄを書き出す
    /// </summary>
    /// <param name="source"></param>
    /// <param name="ex"></param>
    /// <returns>書き出したﾊﾟｽ(失敗したらnull)</returns>
    public static string? Write(string source, Exception ex) => Write(source, ex.ToString());

    /// <summary>
    /// ｸﾗｯｼｭﾚﾎﾟーﾄを書き出す。書けなければ一時ﾌｫﾙﾀﾞーに逃がす。
    /// </summary>
    /// <param name="source"></param>
    /// <param name="detail"></param>
    /// <returns>書き出したﾊﾟｽ(失敗したらnull)</returns>
    public static string? Write(string source, string detail)
    {
        var body = BuildReport(source, detail);
        Debug.WriteLine(body);

        // 開いていれば通常ﾛｸﾞにも残す(Logはﾌﾟﾛﾌｧｲﾙ未ﾛーﾄﾞでも安全)
        try {
            Logger.Dictator.Log(LogLevel.Error, $"{source}{Environment.NewLine}{detail}");
        }
        catch {
            // ﾛｸﾞが死んでてもﾚﾎﾟーﾄは書く
        }

        var fileName = $"{AppName}-{DateTime.Now:yyyyMMdd-HHmmss-fff}-{Environment.ProcessId}.log";
        foreach (var dir in EnumerateReportDirs()) {
            try {
                lock (syncRoot) {
                    Directory.CreateDirectory(dir);
                    var path = Path.Combine(dir, fileName);
                    File.AppendAllText(path, body, Encoding.UTF8);
                    PurgeOldReports(dir);
                    return path;
                }
            }
            catch {
                // 次の候補へ
            }
        }
        return null;
    }

    /// <summary>
    /// ﾚﾎﾟーﾄ置き場の候補
    /// </summary>
    static IEnumerable<string> EnumerateReportDirs()
    {
        string? primary = null;
        try {
            primary = Path.Combine(SystemParams.SettingPouth.FullName, CrashReportDir);
        }
        catch {
            // SystemParamsの初期化自体が死んでいる
        }
        if (primary != null) {
            yield return primary;
        }
        yield return Path.Combine(Path.GetTempPath(), nameof(ImogeIndex), CrashReportDir);
    }

    /// <summary>
    /// 古いﾚﾎﾟーﾄを消す
    /// </summary>
    static void PurgeOldReports(string dir)
    {
        try {
            var olds = new DirectoryInfo(dir)
                .EnumerateFiles($"{AppName}-*.log")
                .OrderByDescending(x => x.Name, StringComparer.Ordinal)
                .Skip(ReportGenerationCount);
            foreach (var f in olds) {
                f.Delete();
            }
        }
        catch {
            // 掃除失敗は無視
        }
    }

    /// <summary>
    /// ﾚﾎﾟーﾄ本文
    /// </summary>
    static string BuildReport(string source, string detail)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"==== {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {source} ====");
        try {
            var asm = Assembly.GetEntryAssembly();
            var version = asm?
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                .InformationalVersion ?? asm?.GetName().Version?.ToString();
            sb.AppendLine($"App     : {AppName} {version}");
            sb.AppendLine($"Runtime : {RuntimeInformation.FrameworkDescription} {RuntimeInformation.ProcessArchitecture}");
            sb.AppendLine($"OS      : {RuntimeInformation.OSDescription}");
            sb.AppendLine($"Thread  : {Environment.CurrentManagedThreadId} {Thread.CurrentThread.Name}");
        }
        catch {
            // 環境情報は取れなくてもよい
        }
        sb.AppendLine();
        sb.AppendLine(detail);
        sb.AppendLine();
        return sb.ToString();
    }
}