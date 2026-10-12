using CommunityToolkit.Mvvm.ComponentModel;
using ImogeIndexWPF.Settingure.Dictators;

namespace ImogeIndexWPF.Settingure.Modelle.Pages;

/// <summary>
/// ﾓﾃﾞｨﾙ
/// </summary>
internal partial class PageModell : ObservableValidator
{
    readonly protected SettingureEditDictator session;
    bool initState = false;

    public PageModell(SettingureEditDictator s)
    {
        session = s;
    }

    /// <summary>
    /// ValidateAllProperties叩き用
    /// </summary>
    /// <returns></returns>
    public virtual bool ValidateAll() {
        ValidateAllProperties();
        return !HasErrors;
    }

    /// <summary>
    /// ﾍﾟーｼﾞのﾀｲﾄﾙ
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// sessionを編集済みに
    /// </summary>
    protected void MarkDirty() {
        if (!initState) {
            session.MarkDirty();
        }
    }

    /// <summary>
    /// 初期化開始扱いに
    /// </summary>
    protected void Sanitizing() {
        initState = true;
    }

    /// <summary>
    /// 初期化完了扱いに
    /// </summary>
    protected void Sanitized() {
        initState = false;
    }

    protected bool WasSanitized => !initState;
}
