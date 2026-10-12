using ImogeIndex;

namespace ImogeIndexWPF.Settingure.Dictators;

/// <summary>
/// 設定繋ぎ込み
/// </summary>
public interface ISettingsJail {
    Settings Settings { get; }

    /// <summary>
    /// 設定を保存して稼働中の設定に反映する
    /// </summary>
    /// <param name="settings">編集中の設定(そのまま稼働中の設定にはしない)</param>
    /// <returns>保存した設定(編集中の設定とは別ｲﾝｽﾀﾝｽ)</returns>
    Task<Settings> SaveAsync(Settings settings);

    /// <summary>
    /// 保存の失敗を通知する
    /// </summary>
    /// <param name="ex"></param>
    void ReportSaveFailure(Exception ex);
}
