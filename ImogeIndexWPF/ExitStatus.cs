namespace ImogeIndexWPF;

/// <summary>
/// 終了ｺーﾄﾞ(自動実行ﾓーﾄﾞ用)
/// </summary>
public enum ExitStatus
{
    /// <summary>
    /// 成功
    /// </summary>
    Success = 0,

    /// <summary>
    /// 失敗(処理ｴﾗー、設定不備、ﾌﾟﾛﾌｧｲﾙ読み込み失敗、未処理例外)
    /// </summary>
    Failure = 1,

    /// <summary>
    /// 引数ｴﾗー
    /// </summary>
    InvalidArguments = 2,

    /// <summary>
    /// 中断
    /// </summary>
    Cancelled = 3,

    /// <summary>
    /// 他のｲﾝｽﾀﾝｽが実行中
    /// </summary>
    AlreadyRunning = 4,
}
