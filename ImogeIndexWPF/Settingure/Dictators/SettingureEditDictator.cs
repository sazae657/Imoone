using CommunityToolkit.Mvvm.ComponentModel;
using ImogeIndex;

namespace ImogeIndexWPF.Settingure.Dictators;

/// <summary>
/// 編集画面持ち回りﾃﾞーﾀ
/// </summary>
internal sealed partial class SettingureEditDictator : ObservableObject
{
    public Settings Original { get; set; }

    [ObservableProperty]
    public partial Settings Draft { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanApply))]
    public partial bool IsDirty {get; set; }

    /// <summary>
    /// 型変換に失敗した等、画面上にﾊﾞｲﾝﾃﾞｨﾝｸﾞｴﾗーが残っている
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanApply))]
    public partial bool HasBindingErrors { get; set; }

    public bool CanApply => IsDirty && !HasBindingErrors;

    public void MarkDirty()
    {
        IsDirty = true;
    }

    public void Reset()
    {
        Draft = Original.DeepCopy();
        IsDirty = false;
    }

    public SettingureEditDictator(Settings original)
    {
        Original = original;
        Draft = original.DeepCopy();
    }
}
