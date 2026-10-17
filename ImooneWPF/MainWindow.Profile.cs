using ImogeIndex;
using ImogeIndex.Common;
using ImogeIndex.Storage;
using ImogeIndex.Territories;
using ImogeIndex.Web6.Committees;
using ImogeIndex.Web6.Storage;
using System.Windows.Controls;
using System.Windows.Threading;

namespace ImooneWPF;

// ﾌﾟﾛﾌｧｲﾙ読み込みとｲﾓｹﾞｲﾝﾃﾞｯｸｽ連動
public partial class MainWindow
{
    readonly ProfileLoader profileLoader = new(ResourcePalabras.Instance);
    bool wasLoadingProfile;

    /// <summary>
    /// 再読み込み要求が読み込み中に来た？
    /// </summary>
    bool reloadRequested;

    readonly IndexingCompletionNotifier.Listener imogeIndexListener = new();

    /// <summary>
    /// ﾌﾟﾛﾌｧｲﾙ選択変更
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private async void ProfileBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!sanitized) {
            return;
        }
        if (Profile.Dictator.ActiveProfile == DC.ActiveProfile) {
            return;
        }
        FileListView.ItemsSource = null;
        Profile.Dictator.SetActive(DC.ActiveProfile);
        await LoadProfile();
    }

    /// <summary>
    /// ﾌﾟﾛﾌｧｲﾙ読み込み
    /// 読み込み中に来た要求は捨てずに、終わってからもう一度読み直す
    /// </summary>
    /// <returns></returns>
    async Task LoadProfile()
    {
        if (wasClosing) {
            return;
        }
        if (wasLoadingProfile) {
            reloadRequested = true;
            return;
        }
        do {
            reloadRequested = false;
            await LoadProfileCore();
        } while (reloadRequested && !wasClosing);
    }

    /// <summary>
    /// ﾌﾟﾛﾌｧｲﾙ読み込み本体
    /// 読み込み中は検索とﾌﾟﾛﾌｧｲﾙ切り替えを止めておく
    /// </summary>
    /// <returns></returns>
    async Task LoadProfileCore()
    {
        wasLoadingProfile = true;
        SearchTextBox.IsEnabled = false;
        DC.IsProfileSwitchable = false;
        try {
            // 読み込みでItemsを作り直すので、走っている検索を先に止める。
            await fileListSearch.QuiesceAsync();
            fileListDisplay.SetSmallDatum(null);
            if (!IndexStore.DatumExists()) {
                ShowErrorDialog(Properties.Resources.MC_E_IndexDatumNotExists);
                return;
            }
            await LoadDatumAsync();
        }
        catch (OperationCanceledException) when (wasClosing) {
            // 閉じられた
        }
        catch (Exception ex) {
            ShowErrorDialog(Properties.Resources.MC_E_DatumCannotOpen.Morfat(ex.Message, ex.StackTrace));
            Close();
        }
        finally {
            SearchTextBox.IsEnabled = true;
            wasLoadingProfile = false;
            DC.IsProfileSwitchable = true;
        }
    }

    /// <summary>
    /// ﾃﾞーﾀﾑを読み込んで一覧に流す
    /// </summary>
    /// <returns></returns>
    async Task LoadDatumAsync()
    {
        DC.NiractionEnabled = false;
        detailPane.Reset();
        try {
            DC.SubProgress = 0.0;
            DC.FileCountText = Properties.Resources.MC_L_Loading;
            var capturedText = DC.FileCountText;
            var capturedPhase = ImogeIndex.Mur.DatumLoadPhase.Sanitize;
            var progress = new Progress<double>(value => DC.SubProgress = value);
            var result = await profileLoader.LoadAsync(progress, (phase, phaseText) => {
                if (capturedPhase != phase) {
                    capturedPhase = phase;
                    Dispatcher.BeginInvoke(() =>
                        DC.SubProgressWasIndeterminate = phase != ImogeIndex.Mur.DatumLoadPhase.Loading);
                }
                if (capturedText != phaseText) {
                    capturedText = phaseText;
                    Dispatcher.BeginInvoke(() => DC.FileCountText = phaseText);
                }
            }, tokenSource.Token);
            fileListDisplay.SetMaxIconSize(FileListSizementEnvuelve.MaxIconSizeOf(Profile.Active.Thumbnail.Enable, Profile.Active.Thumbnail.Size));
            fileListDisplay.SetSmallDatum(Profile.Active.GetThumbnailDatumPath().FullName);
            await fileListSearch.ReloadAsync(result.Items);
            var stale = profileLoader.Datum.StaleMetadataAliases;
            if (stale.Count != 0) {
                // ﾒﾀﾃﾞーﾀﾑが古くてﾒﾓﾘに載ってない
                ShowWarningDialog(Properties.Resources.MC_W_MetadataIndexStale.Morfat(string.Join(", ", stale)));
            }
        }
        finally {
             await Dispatcher.BeginInvoke(() =>{
                 DC.SubProgress = 0.0;
                 DC.SubProgressWasIndeterminate = false;
                 DC.NiractionEnabled = Profile.Active.Niraction.Enable;
                 DC.FileCountText = Properties.Resources.MC_NItems.Morfat(fileListSearch.Items.Count);
             });
        }
    }

    /// <summary>
    /// ﾃﾞーﾀ再読み込み
    /// </summary>
    private async void OnReloadDatum() {
        if (!sanitized || wasClosing) {
            return;
        }
        await LoadProfile();
    }

    /// <summary>
    /// ImogeIndexWPFの更新完了通知(受信ｽﾚｯﾄﾞから来る)
    /// </summary>
    /// <param name="profileName"></param>
    private void ImogeIndexListener_Received(string profileName)
    {
        if (wasClosing || Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished) {
            return;
        }
        try {
            _ = Dispatcher.BeginInvoke(() => OnIndexingCompleted(profileName));
        }
        catch (InvalidOperationException) {
            // 終了処理と行き違った
        }
    }

    /// <summary>
    /// 現在開いているﾌﾟﾛﾌｧｲﾙの更新だけを受けて読み直す
    /// 連続して届いた通知はLoadProfileの再読み込み要求にまとめる
    /// </summary>
    /// <param name="profileName"></param>
    private async void OnIndexingCompleted(string profileName)
    {
        if (!sanitized || wasClosing || !string.Equals(profileName, DC.ActiveProfile, StringComparison.Ordinal)) {
            return;
        }
        await LoadProfile();
    }
}
