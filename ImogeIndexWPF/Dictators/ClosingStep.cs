using ImogeIndex.Web3.Dictators;

namespace ImogeIndexWPF.Dictators;

/// <summary>
/// 終了時の後片付けｼﾞｮﾌﾞ
/// </summary>
internal static class ClosingStep
{
    /// <summary>
    /// 後片付けを1ｽﾃｯﾌﾟ実行
    /// </summary>
    /// <param name="what"></param>
    /// <param name="action"></param>
    public static void Run(string what, Action action)
    {
        try {
            action();
        }
        catch (Exception ex) {
            // 失敗しても記録だけして続行
            Report(what, ex);
        }
    }

    /// <summary>
    /// 失敗記録
    /// </summary>
    /// <param name="what"></param>
    /// <param name="ex"></param>
    public static void Report(string what, Exception ex)
    {
        try {
            CrashDictator.Report($"終了処理に失敗: {what}", ex);
        }
        catch {
            // 記録できなくても終了は止めない
        }
    }
}
