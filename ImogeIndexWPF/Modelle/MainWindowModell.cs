using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using ImogeIndex.Common;
using ImogeIndex.Web3.Dictators;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Media.Imaging;

namespace ImogeIndexWPF.Modelle;


/// <summary>
/// 開く別窓
/// </summary>
internal enum OpenToolType {
    Settingure, RegexSandbox, AboutDialog
}
/// <summary>
/// 別窓を開かせる
/// </summary>
internal class OpenToolMessage {
    public OpenToolMessage(OpenToolType type) {
        Type = type;
    }
    public OpenToolType Type { get; }
}

/// <summary>
/// 正規表現練習帳を開かせる
/// </summary>
internal class OpenRegexSandboxMessage
{
    public OpenRegexSandboxMessage(string? regex)
    {
        Regex = regex;
    }

    /// <summary>
    /// 開いたら即ｾｯﾄする正規表現
    /// </summary>
    public string? Regex { get; }
}

/// <summary>
/// MainWindow
/// </summary>
internal partial class MainWindowModell : ObservableObject {
    /// <summary>
    /// 汎用ﾒｯｾーｼﾞ
    /// </summary>
    [ObservableProperty]
    public partial string? MessageText { get; set; }

    /// <summary>
    /// 大きい方のﾌﾟﾛｸﾞﾚｽﾊﾞー
    /// </summary>
    [ObservableProperty]
    public partial double Progress { get; set; }

    /// <summary>
    /// ﾃﾞﾊﾞｸﾞ(VLMﾚｽﾎﾟﾝｽﾀﾞﾝﾌﾟ用)
    /// </summary>
    [ObservableProperty]
    public partial string? DebugText { get; set; }

    /// <summary>
    /// ﾃﾞﾊﾞｸﾞ画像(VLMﾚｽﾎﾟﾝｽﾀﾞﾝﾌﾟ用)
    /// </summary>
    [ObservableProperty]
    public partial BitmapImage? DebugImg { get; set; }

    /// <summary>
    /// 既知のｴﾗーは飛ばす？
    /// </summary>
    [ObservableProperty]
    public partial bool SkipKnownError { get; set; } = true;

    /// <summary>
    /// ﾒﾀﾃﾞーﾀﾑ再ｽｷｬﾝ？
    /// </summary>
    [ObservableProperty]
    public partial bool RescanMetadata { get; set; } = false;

    /// <summary>
    /// 高速ｽｷｬﾝ？
    /// </summary>
    [ObservableProperty]
    public partial bool QuickScan { get; set; } = false;

    /// <summary>
    /// 更新されたらｷｬﾌﾟｼｮﾝ作り直し
    /// </summary>
    [ObservableProperty]
    public partial bool RecaptionUpdated { get; set; } = true;

    /// <summary>
    /// ﾄﾚｲにぶち込むﾓーﾄﾞ
    /// </summary>
    [ObservableProperty]
    public partial bool MinimizeToTray { get; set; } = false;

    /// <summary>
    /// ﾃーﾏ箱
    /// </summary>
    public ObservableCollection<string> Themes { get; set; } = new(ThemeDisplay.GetThemeNames());

    /// <summary>
    /// ﾏーﾃー
    /// </summary>
    [ObservableProperty]
    public partial string? SelectedTheme { get; set; } = ThemeDisplay.ThemeToString(ThemeDictator.CurrentTheme);

    /// <summary>
    /// 言語箱
    /// </summary>
    public ObservableCollection<LanguageChoice> Languages { get; set; } = new(LanguageDictator.GetLanguageChoices());

    /// <summary>
    /// 言語
    /// </summary>
    [ObservableProperty]
    public partial LanguageChoice? SelectedLanguage { get; set; }


    /// <summary>
    /// ﾌﾟﾛﾌｧｲﾙ選択箱
    /// </summary>
    public ObservableCollection<string> Profiles { get; set; } = new();

    /// <summary>
    /// 現在のﾌﾟﾛﾌｧｲﾙ
    /// </summary>
    [ObservableProperty]
    public partial string? ActiveProfile { get; set; }

    partial void OnActiveProfileChanged(string? value) {
        if (!string.IsNullOrEmpty(value)) {
            OpenSettingMenuHeader = Properties.Resources.ML_OpenSettingsFolder.Morfat(value);
        }
    }

    /// <summary>
    /// 設定変更可能？
    /// </summary>
    [ObservableProperty]
    public partial bool Settingurable { get; set; } = true;

    /// <summary>
    /// 操作ﾛｯｸ掛かってない？
    /// </summary>
    [ObservableProperty]
    public partial bool Operatable { get; set; } = true;

    /// <summary>
    /// 設定開くﾒﾆｭー
    /// </summary>
    [ObservableProperty]
    public partial string OpenSettingMenuHeader { get; set; } = Properties.Resources.ML_OpenSettingsFolder;

    /// <summary>
    /// 開始ﾎﾞﾀﾑ
    /// </summary>
    [RelayCommand]
    private void Start() => OnStartCommand?.Invoke();
    public event Action? OnStartCommand;

    /// <summary>
    /// 設定ﾌｫﾙﾀﾞー開くﾒﾆｭー
    /// </summary>
    [RelayCommand]
    private void OpenSettingFolder() => OnOpenSettingFolder?.Invoke();
    public event Action? OnOpenSettingFolder;

    /// <summary>
    /// ﾌﾟﾛﾌｧｲﾙﾌｫﾙﾀﾞー開くﾒﾆｭー
    /// </summary>
    [RelayCommand]
    private void OpenProfileFolder() => OnOpenProfileFolder?.Invoke();
    public event Action? OnOpenProfileFolder;

    /// <summary>
    /// 選択実行
    /// </summary>
    /// <param name="process"></param>
    [RelayCommand]
    private void RunProzessor(SelectiveProcess process) => OnRunProzessor?.Invoke(process);
    public event Action<SelectiveProcess>? OnRunProzessor;


    /// <summary>
    /// 設定開く
    /// </summary>
    [RelayCommand]
    private void OpenSettingure() {
        if (Settingurable) {
            WeakReferenceMessenger.Default.Send(new OpenToolMessage(OpenToolType.Settingure));
        }
    }

    /// <summary>
    /// 正規表現練習帳開く
    /// </summary>
    [RelayCommand]
    private void OpenRegexSandbox() {
        WeakReferenceMessenger.Default.Send(new OpenToolMessage(OpenToolType.RegexSandbox));
    }

    /// <summary>
    /// ｱﾊﾞｳﾂ
    /// </summary>
    [RelayCommand]
    private void OpenAboutDialog()
    {
        WeakReferenceMessenger.Default.Send(new OpenToolMessage(OpenToolType.AboutDialog));
    }

    /// <summary>
    /// 閉じろ
    /// </summary>
    /// <param name="window"></param>
    [RelayCommand]
    void CloseWindow(Window window) {
        window.Close();
    }
}
