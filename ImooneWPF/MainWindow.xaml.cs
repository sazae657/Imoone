using ImogeIndex;
using ImogeIndex.Common;
using ImogeIndex.Mur;
using ImogeIndex.Storage;
using ImogeIndex.Web3;
using ImogeIndex.Web3.Dialogs;
using ImogeIndex.Web6.Committees;
using ImogeIndex.Web6.Dictators;
using ImogeIndex.Web6.Modelle;
using ImooneWPF.Committees;
using ImooneWPF.Dictators;
using ImooneWPF.Modelle;
using System.Diagnostics;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ImooneWPF;


public partial class MainWindow : ImogeIndex.Web3.Nuendow {
    public MainWindow() : base()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        InitializeComponent();
        fileListColumnJailer = new FileListColumnCommittee(this);
        detailPane = new DetailPaneCommittee<BitmapSource>(DC, ResourcePalabras.Instance, WpfImagePreviewDecoder.Instance,
            bitmap => DC.DetailBitmap = bitmap,
            () => VisualTreeHelper.GetDpi(this).DpiScaleX,
            () => Math.Max(SystemParameters.PrimaryScreenWidth, SystemParameters.PrimaryScreenHeight),
            tokenSource.Token);
        fileListSearch = new FileListSearchCommittee(
            new FileListHost(FileListView, SearchTextBox, fileListColumnJailer), DC, ResourcePalabras.Instance,
            profileLoader.Datum, detailPane.Clear, ex => ShowErrorDialog(ex), tokenSource.Token);
        SearchTextBox.TextChanged += (_, _) => fileListSearch.NotifyQueryChanged();
        SearchTextBox.KeyDown += (_, e) => {
            if (e.Key == Key.Enter && fileListSearch.NotifyEnter()) {
                e.Handled = true;
            }
        };
        saveSettingsTimer.Tick += (_, _) => SaveSettings();
        LoadSettings();
        var fileListGridView = (GridView)FileListView.View;
        fileListDisplay = new FileListDisplayCommittee(FileListView, FileDisplayModeMenu, FileNameColumn,
            SaveFileListColumnSettings,
            new FileListSizementEnvuelve(SettingsDictator.Default.FileListLayout, SettingsDictator.Default.FileIconSize), zoom =>
            {
                // ｻﾑﾈ付きﾘｽﾄではｻﾑﾈの居場所が無くなるのでﾌｧｲﾙ名列を隠させない
                fileListColumnJailer.Discipline(fileListGridView, zoom.Mode == MurDisplayMode.ThumbList ? FileNameColumn : null);
                SettingsDictator.Default.FileListLayout = zoom.Mode;
                SettingsDictator.Default.FileIconSize = zoom.IconSize;
                // ﾎｲーﾙで連打されるので、落ち着いてからまとめて書き出す
                saveSettingsTimer.Stop();
                saveSettingsTimer.Start();
            });
        WireModelEvents();
        InitializeTrayIcon();
        imogeIndexListener.Received += ImogeIndexListener_Received;
        imogeIndexListener.Start();
    }

    MainWindowModell DC => (DataContext as MainWindowModell)!;
    readonly CancellationTokenSource tokenSource = new();
    readonly FileListColumnCommittee fileListColumnJailer;
    readonly FileListDisplayCommittee fileListDisplay;
    readonly FileListSearchCommittee fileListSearch;
    readonly DetailPaneCommittee<BitmapSource> detailPane;

    bool wasClosing;

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        await LoadProfile();
    }

    private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        wasClosing = true;

        aboutWeb3Dialog?.Close();
        aboutWeb3Dialog = null;

        niractionWindow?.Close();
        niractionWindow = null;

        labelEditWindow?.Close();
        labelEditWindow = null;

        fileListSearch.Stop();
        tokenSource.Cancel();
        detailPane.Cancel();
        DC.OnSetDisplayMode -= fileListDisplay.SetPreset;
        fileListDisplay.Dispose();
        ThumbnailStore.Close();
        IndexStore.Close();
        LabelStore.Close();
        SaveSettings();
    }

    private async void Window_Closed(object sender, EventArgs e)
    {
        DisposeTrayIcon();
        imogeIndexListener.Received -= ImogeIndexListener_Received;
        await imogeIndexListener.DisposeAsync();
        tokenSource?.Dispose();
    }

    private void ToggleButton_Loaded(object sender, RoutedEventArgs e)
    {
        var btn = (ToggleButton)sender;

        btn.SetBinding(ToggleButton.IsCheckedProperty, new Binding("IsOpen") { Source = btn.ContextMenu });
        btn.ContextMenu.PlacementTarget = btn;
        btn.ContextMenu.Placement = PlacementMode.Bottom;
    }

    /// <summary>
    /// ﾓﾃﾞﾚのｲﾍﾞﾝﾄ購読
    /// </summary>
    void WireModelEvents()
    {
        DC.OnSetDisplayMode += fileListDisplay.SetPreset;
        DC.OnSearchSimilarImages += SearchSimilarImages;
        DC.OnSimilarHashSwitched += SimilarHash_Switched;
        DC.OnLaunchIndexTool += OnLaunchIndexTool;
        DC.OnReloadDatum += OnReloadDatum;
        DC.OpenFileCommand.Auction += OpenFileCommand_Auction;
        DC.OpenFolderCommand.Auction += OpenFolderCommand_Auction;

        DC.OnOpenSettingFolder += () => OpenSettingFolder_Auction(Profile.Active.SettingFilePath);
        DC.OnOpenProfileFolder += () => OpenSettingFolder_Auction(Profile.Dictator.ProfilePath);

        DC.OnClearDetailPane += detailPane.Clear;
        DC.OnFocusSearchTextBox += () => SearchTextBox.Focus();
        DC.OnOpenAboutDialog += OnOpenAboutDialog;
        DC.OnNiractionResponse += OnNiractionResponse;

        DC.OnOpenLabelEdit += OnOpenLabelEdit;
    }

    private void ShowErrorDialog(Exception e, string? title = null)
    {
        TaskenDialog.Error(this, e.Message, title ?? Properties.Resources.MC_Error);
        Debug.WriteLine(e.Message);
        Debug.WriteLine(e.StackTrace);
    }

    private void ShowErrorDialog(string message, string? title = null) {
        TaskenDialog.Error(this, message, title ?? Properties.Resources.MC_Error);
    }

    private void ShowWarningDialog(string message, string? title = null) {
        TaskenDialog.Warning(this, message, title ?? Properties.Resources.MC_Warning);
    }

    #region ファイルリスト

    private void FileListView_KeyDown(object sender, KeyEventArgs e)
    {
        if ((e.Key != Key.Enter && e.Key != Key.C) || FileListView.SelectedItem is not FileItem selectedData) {
            return;
        }
        e.Handled = true;
        if (e.Key == Key.C) {
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift|ModifierKeys.Control)) {
                try {
                    Clipboard.SetText(selectedData.FullName.AutoQuotePath());
                }
                catch {
                    // いや、どうしようもねえよ
                }
            }
            return;
        }

        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) {
            // Shift押してたらExplorer開ける
            OpenFolderCommand_Auction(selectedData);
        }
        else {
            OpenFileCommand_Auction(selectedData);
        }
    }

    private void ListViewItem_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        var item = sender as ListViewItem;
        if (item == null || item.DataContext == null) {
            return;
        }
        var selectedData = item.DataContext as FileItem;
        if (selectedData == null) {
            return;
        }
        OpenFileCommand_Auction(selectedData);
    }

    private void FileListView_PreviewMouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        var header = (e.OriginalSource as DependencyObject).FindAncestor<GridViewColumnHeader>();
        if (header == null || !fileListColumnJailer.Contains(header.Column)) {
            return;
        }

        var contextMenu = fileListColumnJailer.CreateVisibilityMenu(
            header,
            FileListColumnMenuItem_Click);
        contextMenu.IsOpen = true;
        e.Handled = true;
    }

    private void FileListView_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        var header = (e.OriginalSource as DependencyObject).FindAncestor<GridViewColumnHeader>();
        if (header == null || !fileListColumnJailer.Contains(header.Column)) {
            return;
        }

        // 幅調整や並べ替えで連打されるので、落ち着いてからまとめて書き出す
        saveSettingsTimer.Stop();
        saveSettingsTimer.Start();
    }

    private async void FileListColumnHeader_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not GridViewColumnHeader { Column: not null } header) {
            return;
        }
        if (!fileListSearch.CanResort || !fileListColumnJailer.SortBy(header.Column)) {
            return;
        }
        await fileListSearch.ResortAsync();
    }

    private void FileListColumnMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem menuItem || menuItem.Tag is not int index) {
            return;
        }

        if (FileListView.View is not GridView gridView ||
            !fileListColumnJailer.SetVisibility(gridView, index, menuItem.IsChecked))
        {
            menuItem.IsChecked = true;
            return;
        }
        SaveSettings();
    }

    private async void FileListView_ContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        if (sender is not ListViewItem item || item.DataContext is not FileItem fileItem)
        {
            return;
        }

        BuildLabelMenu(item.ContextMenu, fileItem);

        var image = item.ContextMenu.Items.OfType<Image>().FirstOrDefault();
        if (image == null)
        {
            return;
        }
        await detailPane.FillThumbnailAsync(fileItem, bitmap => image.Source = bitmap,
            () => ReferenceEquals(item.DataContext, fileItem));
    }

    private void FileListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (fileListSearch.IsSwappingItemsSource)
        {
            // 一覧差し替えに伴う選択の付け外しなので詳細ﾍﾟｲﾝは触らない
            return;
        }
        if (sender is not ListView view || view.SelectedItem is not FileItem fileItem)
        {
            detailPane.Cancel();
            return;
        }
        detailPane.Show(fileItem);
    }

    #endregion
}
