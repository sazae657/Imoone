using ImogeIndex;
using ImogeIndex.Web3.Dialogs;
using ImogeIndex.Web3.Dictators;
using ImogeIndex.Web6.Dictators;
using System.Windows.Controls;
using System.Windows.Threading;

namespace ImooneWPF;

// 設定の保存と読み込み
public partial class MainWindow
{
    /// <summary>
    /// 列ﾍｯﾀﾞー操作の設定保存を間引く
    /// </summary>
    readonly DispatcherTimer saveSettingsTimer = new()
    {
        Interval = TimeSpan.FromSeconds(2)
    };

    bool sanitized = false;

    /// <summary>
    /// ﾃーﾏ変更
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void ThemeBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!sanitized)
        {
            return;
        }
        var theme = ThemeDisplay.StringToTheme(DC.SelectedTheme);
        ThemeDictator.ApplyTheme(theme);
        SettingsDictator.Default.Theme = theme.ToString();
        SettingsDictator.Save();
    }

    /// <summary>
    /// 言語変更(再起動で反映)
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void LanguageBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!sanitized || DC.SelectedLanguage is not { } language)
        {
            return;
        }
        if (language.Code == LanguageDictator.Normalize(SettingsDictator.Default.Language))
        {
            return;
        }
        SettingsDictator.Default.Language = language.Code;
        SettingsDictator.Save();
        TaskenDialog.Info(this, ImogeIndex.Web3.Properties.Resources.LNG_RestartRequired, Properties.Resources.MC_AppTitle);
    }

    /// <summary>
    /// 設定保存
    /// </summary>
    void SaveSettings()
    {
        saveSettingsTimer.Stop();
        SaveFileListColumnSettings();
        if (DC.ShowDetailPane)
        {
            SettingsDictator.Default.DetailPane.Width = Math.Max(64, (int)Grod.ColumnDefinitions[2].ActualWidth);
        }
        SettingsDictator.Default.DetailPane.Visible = DC.ShowDetailPane;
        SettingsDictator.Default.DetailPane.ShowOriginal = DC.DetailPaneShowOriginal;

        SettingsDictator.Default.WindowParamOf(WindowParamsKey.Main, param => {
            param.Width = Math.Max(64, (int)Width);
            param.Height = Math.Max(64, (int)Height);
        });

        SettingsDictator.Default.LastProfile = DC.ActiveProfile;
        SettingsDictator.Default.Theme = ThemeDisplay.StringToTheme(DC.SelectedTheme).ToString();
        SettingsDictator.Default.IncrementalSearch = DC.IncrementalSearch;
        SettingsDictator.Default.MinimizeToTray = DC.MinimizeToTray;
        SettingsDictator.Default.SimilarHashMoone = DC.SimilarHashes;
        SettingsDictator.Save();
    }

    /// <summary>
    /// ﾘｽﾄのｶﾗﾑ状態保存
    /// </summary>
    void SaveFileListColumnSettings()
    {
        if (FileListView.View is GridView gridView)
        {
            fileListColumnJailer.CaptureSettings(gridView);
        }
    }

    /// <summary>
    /// 設定読み込み
    /// </summary>
    void LoadSettings()
    {
        Profile.Load();
        SettingsDictator.Load(App.SettingsPartition);
        var theme = ThemeDisplay.StringToTheme(SettingsDictator.Default.Theme);
        ThemeDictator.ApplyTheme(theme);
        DC.SelectedTheme = ThemeDisplay.ThemeToString(theme);
        var language = LanguageDictator.Normalize(SettingsDictator.Default.Language);
        DC.SelectedLanguage = DC.Languages.FirstOrDefault(x => x.Code == language);

        DC.DetailThumbnailSize = Profile.Active.Thumbnail.Size;

        SettingsDictator.Default.WindowParamOf(WindowParamsKey.Main, param => {
            Width = Math.Max(64, param.Width);
            Height = Math.Max(64, param.Height);
        });

        if (FileListView.View is GridView gridView)
        {
            fileListColumnJailer.Initialize(gridView);
        }
        DC.ShowDetailPane = SettingsDictator.Default.DetailPane.Visible;
        DC.DetailPaneWidth = SettingsDictator.Default.DetailPane.Width;
        DC.DetailPaneShowOriginal = SettingsDictator.Default.DetailPane.ShowOriginal;
        DC.IncrementalSearch = SettingsDictator.Default.IncrementalSearch;
        DC.MinimizeToTray = SettingsDictator.Default.MinimizeToTray;
        DC.SimilarHashes = SettingsDictator.Default.SimilarHashMoone;
        foreach (var x in Profile.Dictator.Profiles)
        {
            DC.Profiles.Add(x.Key);
        }
        if (!string.IsNullOrEmpty(SettingsDictator.Default.LastProfile) &&
            DC.Profiles.Contains(SettingsDictator.Default.LastProfile))
        {
            DC.ActiveProfile = SettingsDictator.Default.LastProfile;
            Profile.Dictator.SetActive(DC.ActiveProfile);
        }
        else
        {
            DC.ActiveProfile = Profile.Dictator.ActiveProfile;
        }
        sanitized = true;
    }

}
