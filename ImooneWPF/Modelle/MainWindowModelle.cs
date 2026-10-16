using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ImogeIndex.Web6.Modelle;
using ImogeIndex.Web3.Dictators;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Media.Imaging;

namespace ImooneWPF.Modelle;

/// <summary>
/// ﾓﾃﾞﾚ(WPF依存部分、本体はImooneModell)
/// </summary>
public partial class MainWindowModell : ImooneModell
{
    public MainWindowModell() : base(ResourcePalabras.Instance)
    {
    }

    /// <summary>
    /// ﾏーﾃーﾘｽﾄ
    /// </summary>
    public ObservableCollection<string> Themes { get; set; } = new(ThemeDisplay.GetThemeNames());

    /// <summary>
    /// ﾏーﾃー
    /// </summary>
    [ObservableProperty]
    public partial string SelectedTheme { get; set; } = ImogeIndex.Web3.Properties.Resources.APT_System;

    /// <summary>
    /// 言語ﾘｽﾄ
    /// </summary>
    public ObservableCollection<LanguageChoice> Languages { get; set; } = new(LanguageDictator.GetLanguageChoices());

    /// <summary>
    /// 言語
    /// </summary>
    [ObservableProperty]
    public partial LanguageChoice? SelectedLanguage { get; set; }

    /// <summary>
    /// 詳細(ryのﾌﾟﾚﾋﾞｭー画像
    /// </summary>
    [ObservableProperty]
    public partial BitmapSource? DetailBitmap { get; set; }

    /// <summary>
    /// ｸﾘｯﾌﾟﾎﾞーﾄﾞに文字列を入れる
    /// </summary>
    /// <param name="text"></param>
    protected override void SetClipboardText(string text) => Clipboard.SetText(text);

    /// <summary>
    /// ｳｲﾝﾄﾞｳ閉じろ
    /// </summary>
    /// <param name="window"></param>
    [RelayCommand]
    void CloseWindow(Window window) {
        window.Close();
    }
}
