namespace ImogeIndexWPF.Dictators;

/// <summary>
/// IndexingRunnerから画面への通知
/// </summary>
internal interface IIndexingHost
{
    /// <summary>
    /// ﾒｯｾーｼﾞ表示
    /// </summary>
    void ShowMessage(string message);

    /// <summary>
    /// ﾒｲﾝのﾌﾟﾛｸﾞﾚｽ表示
    /// </summary>
    void ShowProgress(double progress);

    /// <summary>
    /// ｴﾗー表示
    /// </summary>
    void ReportError(string message, Exception? exception);

    /// <summary>
    /// Taggerの設定がおかしい
    /// </summary>
    void ReportTaggerMisconfiguration();

    /// <summary>
    /// ｳﾈｳﾈの開始/停止
    /// </summary>
    void SetBusy(bool busy);

    /// <summary>
    /// 小さい方のﾌﾟﾛｸﾞﾚｽを初期状態に戻す
    /// </summary>
    void ResetSubProgress();

    /// <summary>
    /// ﾌﾟﾛｾｯｻーからのﾒｯｾーｼﾞ
    /// </summary>
    void PostMessage(string message);

    /// <summary>
    /// ﾌﾟﾛｾｯｻーからのﾒｲﾝの進捗
    /// </summary>
    void PostProgress(int current, int total, int error, string phase, string message);

    /// <summary>
    /// ﾌﾟﾛｾｯｻーからの小さい方の進捗
    /// </summary>
    void PostSubProgress(int index, double progress);

    /// <summary>
    /// ﾌﾟﾛｾｯｻーからのすごく長い処理の開始/終了
    /// </summary>
    void PostTremendouslyLongProcess(bool busy);

    /// <summary>
    /// ｷｬﾌﾟｼｮﾝ受信
    /// </summary>
    /// <param name="sentImage">実際にVLMへ送信したPNG(不明ならnull)</param>
    void PostCaption(TimeSpan elapsed, string filePath, byte[]? sentImage, string content);
}
