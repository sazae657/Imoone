using ImogeIndex;
using ImogeIndex.Common;
using ImogeIndex.Web3.Dialogs;
using ImogeIndex.Web3.Dictators;
using ImogeIndexWPF.Dictators;
using ImogeIndexWPF.Modelle;
using ImogeIndexWPF.Settingure.Modelle.Pages;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media.Imaging;


namespace ImogeIndexWPF;

public partial class MainWindow : ImogeIndex.Web3.Nuendow, IIndexingHost {
    public MainWindow(LaunchOptions opts) {
        launchOptions = opts;
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        InitializeComponent();
        SetupSubProgressContainer(1);
        LoadSettings();
        trayIcon = new TrayIconDictator(
            Dispatcher,
            RestoreFromTray,
            OnStartBittan,
            RequestStop,
            Close,
            GetTrayStatus);

        if (opts.NoWindow) {
            // NWの場合はﾁﾗ見え対策で最小化ｽﾀーﾄ
            WindowState = WindowState.Minimized;
            ShowActivated = false;
            ShowInTaskbar = false;
        }

        StateChanged += OnWindowStateChanged;
        Closed += (_, _) => trayIcon.Dispose();
        runner = new IndexingRegiment(this, state);
        subWindows = new SubWindowDictator(this);
        uneune = new UneuneDictator(Dispatcher, SetSubProgressValue);
        state.Changed += RefreshOperationControls;
        RefreshOperationControls();
    }

    readonly TrayIconDictator trayIcon;
    WindowState restoreState = WindowState.Normal;

    readonly IndexerStateDictator state = new();
    Task? processingTask;

    /// <summary>
    /// 自動実行の結果(=終了ｺーﾄﾞ)
    /// </summary>
    public ExitStatus ExitStatus { get; private set; } = ExitStatus.Success;

    readonly SubWindowDictator subWindows;

    LaunchOptions launchOptions;
    readonly IndexingRegiment runner;
    readonly UneuneDictator uneune;

    MainWindowModell DC => (MainWindowModell)DataContext;

    List<IncommingProgressBar> subProgressBars = new();

    private async void Window_Loaded(object sender, RoutedEventArgs e) {
        if (!state.TryBeginInitialize()) {
            return;
        }

        MinHeight = ActualHeight;
        MinWidth = WarpDefaults.MainWindowWidth;

        if (launchOptions.NoWindow && WindowState == WindowState.Minimized) {
            // 最小化状態来るとStateChangedが来ないのでここでﾄｲﾚへ送る
            if (trayIcon.TryShow())
            {
                Hide();
            }
            else
            {
                RestoreFromTray();
            }
        }

        subWindows.RegisterMessages();
        try {
            Profile.Load();
            if (!string.IsNullOrEmpty(launchOptions.Profile)) {
                Profile.Dictator.SetActive(launchOptions.Profile);
            }

            DC.Profiles.Clear();
            foreach (var m in Profile.Dictator.Profiles.Keys) {
                DC.Profiles.Add(m);
            }
            DC.ActiveProfile = Profile.Dictator.ActiveProfile;

            DC.OnOpenProfileFolder += () => ShowInExplorer(Profile.Dictator.ProfilePath?.FullName);
            DC.OnOpenSettingFolder += () => ShowInExplorer(Profile.Active.SettingFilePath?.FullName);
            DC.OnRunProzessor += OnRunProzessor;
            DC.OnStartCommand += OnStartBittan;
            DC.QuickScan = launchOptions.QuickScan;
        }
        catch (Exception re) {
            ShowProcessMessage(Properties.Resources.WG_Eroare.Morfat(re.Message), re);
            ExitStatus = ExitStatus.Failure;
            Close();
            return;
        }

        state.CompleteInitialize();

        if (launchOptions.Auto) {
            processingTask = RunAutoProcess();
            await processingTask;
            Close();
        }
    }

    /// <summary>
    /// 自動実行して結果を終了ｺーﾄﾞに残す
    /// </summary>
    async Task RunAutoProcess()
    {
        try {
            ExitStatus = await ExecuteProcess(ProcessPlan.Full);
        }
        catch (Exception r) {
            ShowProcessMessage(Properties.Resources.WG_Eroare.Morfat(r.Message), r);
            ExitStatus = ExitStatus.Failure;
        }
    }


    private async void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        if (state.IsClosed) {
            return;
        }
        e.Cancel = true;
        // 以降の後片付けは途中で失敗しても必ず最後まで進めて閉じる
        if (!state.TryBeginClose()) {
            return;
        }
        try {
            runner.Cancel();
            subWindows.CloseAll();
            ClosingStep.Run("SaveSettings", SaveSettings);

            if (processingTask != null) {
                try {
                    await processingTask;
                }
                catch {
                    // 無視
                }
            }
            await runner.Withdrawal();
        }
        catch (Exception ex) {
            ClosingStep.Report("Window_Closing", ex);
        }
        finally {
            state.MarkClosed();
            try {
                runner.Dispose();
            }
            catch {
                // 閉じるのを優先する
            }
            // Closing中に同期でCloseすると例外になるので別途ｽｹｼﾞｭーﾙ
            _ = Dispatcher.BeginInvoke(Close);
        }
    }

    /// <summary>
    /// 小ﾌﾟﾛｸﾞﾚｽﾊﾞー設定
    /// </summary>
    /// <param name="count"></param>
    void SetupSubProgressContainer(int count)
    {
        if (count < 0) {
            SubProgressContainer.Children.Clear();
            subProgressBars.Clear();
            var sp = new IncommingProgressBar();
            subProgressBars.Add(sp);
            SubProgressContainer.Children.Add(sp);
            this.SizeToContent = SizeToContent.Height;
            return;
        }

        for (int i = subProgressBars.Count; i < count; i++) {
            var sp = new IncommingProgressBar();
            subProgressBars.Add(sp);
            SubProgressContainer.Children.Add(sp);
        }
        this.SizeToContent = SizeToContent.Height;
    }

    /// <summary>
    /// 小ﾌﾟﾛｸﾞﾚｽに値を入れる
    /// </summary>
    /// <param name="value"></param>
    void SetSubProgressValue(double value)
    {
        if (subProgressBars.Count > 0) {
            subProgressBars[0].Value = value;
        }
    }

    /// <summary>
    /// 操作可能/不可能の設定
    /// </summary>
    void RefreshOperationControls()
    {
        var canStart = state.CanStart;
        var canStop = state.CanStop;
        DC.Operatable = canStart || canStop;
        // 実行中、初期化中、終了処理中は設定を触らせない
        DC.Settingurable = canStart;
        StartBittan.Content = state.IsProcessing ? Properties.Resources.IDM_STOP : Properties.Resources.IDM_START;
        trayIcon.SetCommands(canStart, canStop, !state.IsClosing);
    }

    /// <summary>
    /// 停止要求
    /// </summary>
    void RequestStop()
    {
        if (!state.TryRequestStop()) {
            return;
        }
        DC.MessageText = Properties.Resources.WG_Stopping;
        runner.Cancel();
    }

    /// <summary>
    /// ﾒｲﾝﾌﾟﾛｾｽ実行
    /// </summary>
    /// <returns></returns>
    async Task<ExitStatus> ExecuteProcess(ProcessPlan plan)
    {
        if (state.IsClosing) {
            return ExitStatus.Cancelled;
        }
        if (!state.TryBeginProcess()) {
            return ExitStatus.Failure;
        }
        try {
            return await runner.Advance(plan, CreateIndexingOptions());
        }
        finally {
            state.EndProcess();
        }
    }

    /// <summary>
    /// 画面の状態から実行ｵﾌﾟｼｮﾝを作る
    /// </summary>
    /// <returns></returns>
    IndexingOptions CreateIndexingOptions()
    {
        return new(
            RescanMetadata: DC.RescanMetadata,
            QuickScan: launchOptions.Auto ? launchOptions.QuickScan : DC.QuickScan,
            RecaptionUpdated: DC.RecaptionUpdated,
            SkipKnownError: DC.SkipKnownError,
            RethrowErrors: launchOptions.Auto);
    }

    /// <summary>
    /// ﾒｯｾーｼﾞﾎﾞｯｸｽ表示
    /// </summary>
    /// <param name="massage"></param>
    /// <param name="exception"></param>
    void ShowProcessMessage(string massage, Exception? exception)
    {
        if (launchOptions.Auto) {
            // 自動実行中はﾀﾞｲｱﾛｸﾞもｳｲﾝﾄﾞｳも出さず、記録だけ残す
            CrashDictator.Report(massage, exception);
            return;
        }
        if (!IsVisible) {
            RestoreFromTray();
        }
        if (exception == null || !WarpSettings.Default.ShowDetailedError) {
            TaskenDialog.Error(this, massage, Properties.Resources.WG_Error);
        } else {
            var msg = exception.StackTrace ?? exception.Message;
            TaskenDialog.Error(this, msg, Properties.Resources.WG_Error, massage);
        }
    }


    /// <summary>
    /// ｽﾀーﾄ
    /// </summary>
    private async void OnStartBittan() {
        if (!DC.Operatable || state.IsClosing) {
            return;
        }
        if (state.IsProcessing) {
            RequestStop();
            return;
        }
        if (!CanBeginProcess()) {
            return;
        }
        try {
            processingTask = ExecuteProcess(ProcessPlan.Full);
            await processingTask;
        }
        catch (Exception ex) {
            ShowProcessMessage(ex.Message, ex);
        }
    }

    /// <summary>
    /// 処理を開始してよいか確認する
    /// </summary>
    /// <returns></returns>
    bool CanBeginProcess()
    {
        if (!state.CanStart) {
            return false;
        }
        if (subWindows.IsSettingureOpen) {
            // 編集中の設定と実行中の設定が食い違うので開始させない
            RestoreFromTray();
            subWindows.ActivateSettingure();
            ShowProcessMessage(Properties.Resources.WG_E_CloseTheSettingure, null);
            return false;
        }
        return true;
    }

    /// <summary>
    /// ﾌﾟﾛﾌｧｲﾙの選択変更
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void Profiles_SelectionChanged(object sender, SelectionChangedEventArgs e) {
        var kb = sender as ComboBox;
        if (kb == null) {
            return;
        }
        subWindows.CloseSettingure();
        try {
            Profile.Dictator.SetActive(DC.Profiles[kb.SelectedIndex]);
        }
        catch (Exception ex) {
            ShowProcessMessage(ex.Message, ex);
        }
    }

    /// <summary>
    /// 選択したﾌﾟﾛｾｯｻー実行ｵﾌﾞﾒﾆｭー
    /// </summary>
    /// <param name="proc"></param>
    private async void OnRunProzessor(SelectiveProcess proc)
    {
        if (!CanBeginProcess()) {
            return;
        }
        try {
            processingTask = ExecuteProcess(ProcessPlan.FromSelective(proc));
            await processingTask;
        }
        catch (Exception ex) {
            ShowProcessMessage(ex.Message, ex);
        }
    }

    #region IIndexingHost
    void IIndexingHost.ShowMessage(string message)
    {
        DC.MessageText = message;
    }

    void IIndexingHost.ShowProgress(double progress)
    {
        DC.Progress = progress;
    }

    void IIndexingHost.ReportError(string message, Exception? exception)
    {
        ShowProcessMessage(message, exception);
    }

    void IIndexingHost.ReportTaggerMisconfiguration()
    {
        ShowProcessMessage(Properties.Resources.WG_E_InvalidTaggerSetting, null);
        if (!launchOptions.Auto) {
            // Tagger設定に飛ばす
            subWindows.OpenSettingure(typeof(TaggerPageModell));
        }
    }

    void IIndexingHost.SetBusy(bool busy)
    {
        if (busy) {
            uneune.Start();
        }
        else {
            uneune.Stop();
        }
    }

    void IIndexingHost.ResetSubProgress()
    {
        SetupSubProgressContainer(-1);
    }

    void IIndexingHost.PostMessage(string message)
    {
        Auction(() => DC.MessageText = message);
    }

    void IIndexingHost.PostProgress(int current, int total, int error, string phase, string message)
    {
        Auction(() => {
            DC.MessageText = $"{phase} {current}/{total} ({error}) {message}";
            DC.Progress = (double)current / total;
        });
    }

    void IIndexingHost.PostSubProgress(int index, double progress)
    {
        Auction(() => {
            if (index < 0) {
                SetupSubProgressContainer(-1);
                return;
            }
            SetSubProgressValue(progress);
        });
    }

    void IIndexingHost.PostTremendouslyLongProcess(bool busy)
    {
        // UIｽﾚｯﾄﾞに渡すだけにする
        if (busy) {
            Dispatcher.BeginInvoke(uneune.Start);
        }
        else {
            Dispatcher.BeginInvoke(uneune.Stop);
        }
    }

    void IIndexingHost.PostCaption(TimeSpan elapsed, string filePath, byte[]? sentImage, string content)
    {
        var image = CreatePreviewImage(filePath, sentImage);
        var sent = sentImage == null ? "" :  $"\n{Properties.Resources.WG_D_VLM2.Morfat(image.PixelWidth, image.PixelHeight, sentImage.Length)}";
        Auction(() => {
            DC.DebugImg = image;
            DC.DebugText = $"{elapsed.TotalSeconds:F2} sec\n{filePath}{sent}\n{content}";
        });
    }


    /// <summary>
    /// VLMﾌﾟﾚﾋﾞｭー画像を作る
    /// </summary>
    /// <param name="filePath"></param>
    /// <param name="sentImage"></param>
    /// <returns></returns>
    static BitmapImage CreatePreviewImage(string filePath, byte[]? sentImage)
    {
        var image = new BitmapImage();
        image.BeginInit();
        if (sentImage != null) {
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = new MemoryStream(sentImage);
        }
        else {
            // 送信画像が撮れてなかったらﾃﾞｨｽｸ上の画像
            image.UriSource = new Uri(filePath);
        }
        image.EndInit();
        image.Freeze();
        return image;
    }
    #endregion

    /// <summary>
    /// Expanderが閉じた時にSizeToContentをSizeToContent.Heightに戻す
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void Expander_Collapsed(object sender, RoutedEventArgs e)
    {
        this.SizeToContent = SizeToContent.Height;
    }

    /// <summary>
    /// ﾄｸﾞﾙﾎﾞﾀﾝ式ﾌﾟﾙﾀﾞｳﾝ
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void ToggleButton_Loaded(object sender, RoutedEventArgs e)
    {
        var btn = (ToggleButton)sender;

        btn.SetBinding(ToggleButton.IsCheckedProperty, new Binding("IsOpen") { Source = btn.ContextMenu });
        btn.ContextMenu.PlacementTarget = btn;
        btn.ContextMenu.Placement = PlacementMode.Bottom;
    }

    /// <summary>
    /// pathをExplorerで開く
    /// </summary>
    /// <param name="path"></param>
    void ShowInExplorer(string? path)
    {
        if (string.IsNullOrEmpty(path)) {
            return;
        }
        try {
            var psi = new ProcessStartInfo {
                FileName = "explorer.exe",
                UseShellExecute = true,
                Arguments = "/select,\"" + path + "\""
            };

            Process.Start(psi);
        }
        catch (Exception ex) {
            ShowProcessMessage(Properties.Resources.WG_Eroare.Morfat(ex.Message), ex);
        }
    }

    /// <summary>
    /// 重量級Dispatch
    /// </summary>
    /// <param name="action"></param>
    void Auction(Action action)
    {
        try {
            _ = Dispatcher.BeginInvoke(() => {
                try {
                    action();
                }
                catch {
                }
            });
        }
        catch { }
    }

    /// <summary>
    /// これの設定自動読み込み
    /// </summary>
    void LoadSettings()
    {
        DC.SelectedTheme = ThemeDisplay.ThemeToString(ThemeDictator.CurrentTheme);
        var language = LanguageDictator.Normalize(WarpSettings.Default.Language);
        DC.SelectedLanguage = DC.Languages.FirstOrDefault(x => x.Code == language);
        DC.MinimizeToTray = WarpSettings.Default.MinimizeToTray;

        WarpSettings.Default.WindowParamOf(WindowParamsKey.Main, mwp => {
            Width = Math.Max(WarpDefaults.MainWindowWidth, mwp.Width);
            // 原状高さは復元しない
            // Height = Math.Max(WarpDefaults.MainWindowHeight, mwp.Height);
        });
    }

    /// <summary>
    /// これの設定保存
    /// </summary>
    void SaveSettings() {
        WarpSettings.Default.WindowParamOf(WindowParamsKey.Main, mwp => {
            mwp.Width = Math.Max(WarpDefaults.MainWindowWidth, (int)Width);
            mwp.Height = Math.Max(WarpDefaults.MainWindowHeight, (int)Height);
        });

        WarpSettings.Default.MinimizeToTray = DC.MinimizeToTray;
        WarpSettings.Save();
    }

    bool synchronizingTheme;

    private void SettingsMenu_Opened(object sender, RoutedEventArgs e)
    {
        SynchronizeThemeSelection();
    }

    private void ThemeBox_Loaded(object sender, RoutedEventArgs e)
    {
        SynchronizeThemeSelection();
    }

    /// <summary>
    /// 適用中のﾃーﾏから選択を復元する
    /// </summary>
    private void SynchronizeThemeSelection()
    {
        synchronizingTheme = true;
        try {
            ThemeBox.GetBindingExpression(ItemsControl.ItemsSourceProperty)?.UpdateTarget();
            DC.SelectedTheme = ThemeDisplay.ThemeToString(ThemeDictator.CurrentTheme);
            ThemeBox.GetBindingExpression(Selector.SelectedItemProperty)?.UpdateTarget();
        }
        finally {
            synchronizingTheme = false;
        }
    }

    /// <summary>
    /// ﾃーﾏ箱
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void ThemeBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (synchronizingTheme || sender is not ComboBox { SelectedItem: string name }) {
            return;
        }
        var theme = ThemeDisplay.StringToTheme(name);
        if (theme != ThemeDictator.CurrentTheme)
        {
            ThemeDictator.ApplyTheme(theme);
            WarpSettings.Default.Theme = theme.ToString();
            WarpSettings.Save();
        }
    }

    /// <summary>
    /// 言語箱(再起動で反映)
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void LanguageBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is not ComboBox { SelectedItem: LanguageChoice language }) {
            return;
        }
        if (language.Code == LanguageDictator.Normalize(WarpSettings.Default.Language)) {
            return;
        }
        WarpSettings.Default.Language = language.Code;
        WarpSettings.Save();
        TaskenDialog.Info(this, ImogeIndex.Web3.Properties.Resources.LNG_RestartRequired, Properties.Resources.ML_AppTitle);
    }

    #region タスクトレイ関連

    /// <summary>
    /// 最小化された時にｺﾞﾆｮｺﾞﾆｮする
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    void OnWindowStateChanged(object? sender, EventArgs e)
    {
        if (WindowState != WindowState.Minimized) {
            restoreState = WindowState;
            trayIcon.Hide();
            return;
        }
        if (state.IsClosing) {
            return;
        }
        if (launchOptions.NoWindow || DC.MinimizeToTray) {
            if (trayIcon.TryShow())
            {
                Hide();
            }
            else
            {
                RestoreFromTray();
            }
        }
    }

    /// <summary>
    /// ﾄﾚｲから戻す
    /// </summary>
    public void RestoreFromTray()
    {
        if (state.IsClosed) {
            return;
        }
        // --no-windowで起動した時に消しておいたのを戻す
        ShowInTaskbar = true;
        ShowActivated = true;
        Show();
        if (WindowState == WindowState.Minimized) {
            WindowState = restoreState;
        }
        trayIcon.Hide();
        Activate();
    }

    /// <summary>
    /// ﾄﾚｲｱｲｺﾝのﾂーﾙﾁｯﾌﾟ用文字列作成
    /// </summary>
    /// <returns></returns>
    string GetTrayStatus()
    {
        string status;
        if (state.IsClosing) {
            status = Properties.Resources.TM_ShuttingDown;
        }
        else if (state.IsStopping) {
            status = Properties.Resources.TM_Stopping;
        }
        else if (state.IsProcessing) {
            status = Properties.Resources.TM_Running;
        }
        else {
            status = Properties.Resources.TM_Idle;
        }
        var progress = state.IsProcessing && double.IsFinite(DC.Progress) && DC.Progress > 0
            ? $" {Math.Clamp(DC.Progress, 0, 1):P0}" : "";
        return $"{Properties.Resources.ML_AppTitle}: {status}{progress} {DC.MessageText}";
    }

    #endregion
}
