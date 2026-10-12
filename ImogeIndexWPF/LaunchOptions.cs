namespace ImogeIndexWPF;

/// <summary>
/// 起動ｵﾌﾟｼｮﾝ
/// </summary>
/// <param name="Auto">自動実行</param>
/// <param name="Profile">ﾌﾟﾛﾌｧｲﾙ</param>
/// <param name="AllowMultiple">ﾓーﾁﾎﾟー</param>
/// <param name="QuickScan">高速ｽｷｬﾝ</param>
/// <param name="NoWindow">ｳｲﾝﾄﾞｳ表示しない</param>
public sealed record LaunchOptions(
    bool Auto,
    string? Profile,
    bool AllowMultiple,
    bool QuickScan,
    bool NoWindow
);
