using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ImogeIndex.Common;
using ImogeIndex.Web6.Storage;
using ImogeIndex.Mur;
using ImogeIndex.Niraction;
using ImogeIndex.Storage;
using System.Collections.ObjectModel;

namespace ImogeIndex.Web6.Modelle;

/// <summary>
/// 一覧の表示方式
/// </summary>
public enum MurDisplayMode {
    List,
    ThumbList,
    Icon,
}

/// <summary>
/// 表示方式のﾌﾟﾘｾｯﾄ(ﾒﾆｭーとｼｮーﾄｶｯﾄ用)
/// </summary>
public enum MurDisplayPreset {
    List,
    ThumbList,
    IconS,
    IconM,
    IconL,
    IconH,
}

/// <summary>
/// ﾒｲﾝｳｲﾝﾄﾞｳのﾓﾃﾞﾚ
/// ﾃーﾏや画像やｸﾘｯﾌﾟﾎﾞーﾄﾞなどUIﾌﾚーﾑﾜーｸに依存するものは派生側で持つ
/// </summary>
public abstract partial class ImooneModell : ObservableObject
{
    /// <summary>
    /// 表示文言
    /// </summary>
    protected IImoonePalabras Texts { get; }

    protected ImooneModell(IImoonePalabras texts)
    {
        Texts = texts;
        OpenSettingMenuHeader = texts.OpenSettingsFolder(null);
    }

    /// <summary>
    /// ﾌﾟﾛﾌｧｲﾙﾘｽﾄ
    /// </summary>
    public ObservableCollection<string> Profiles { get; set; } = new();

    /// <summary>
    /// ﾌﾟﾛﾌｧｲﾙ変更可能？
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SearchSimilarImagesCommand))]
    public partial bool IsProfileSwitchable { get; set; } = false;

    /// <summary>
    /// 現在のﾌﾟﾛﾌｧｲﾙ
    /// </summary>
    [ObservableProperty]
    public partial string ActiveProfile { get; set; } = string.Empty;
    partial void OnActiveProfileChanged(string value)
    {
        if (!string.IsNullOrEmpty(value)) {
            OpenSettingMenuHeader = Texts.OpenSettingsFolder(value);
        }
    }

    /// <summary>
    /// 設定開くﾒﾆｭーのﾗﾍﾞﾙ
    /// </summary>
    [ObservableProperty]
    public partial string OpenSettingMenuHeader { get; set; } = string.Empty;


    /// <summary>
    /// ﾌｧｲﾙ数表示ｴﾘｱ
    /// </summary>
    [ObservableProperty]
    public partial string? FileCountText { get; set; }

    /// <summary>
    /// 類似検索中？
    /// </summary>
    [ObservableProperty]
    public partial bool IsSimilarSearch { get; set; }
    partial void OnIsSimilarSearchChanged(bool value)
    {
        SearchPlaceholder = value ? Texts.SimilarSearchHint : string.Empty;
    }

    /// <summary>
    /// 類似検索欄のﾗﾍﾞﾙ
    /// </summary>
    [ObservableProperty]
    public partial string SimilarSearchText { get; set; } = string.Empty;

    /// <summary>
    /// 検索ﾎﾞｯｸｽのﾌﾟﾚーｽﾎﾙﾀﾞー（空文字なら非表示）
    /// </summary>
    [ObservableProperty]
    public partial string SearchPlaceholder { get; set; } = string.Empty;

    /// <summary>
    /// 小さい方のﾌﾟﾛｸﾞﾚｽﾊﾞーのあたい
    /// </summary>
    [ObservableProperty]
    public partial double SubProgress { get; set; } = 0;

    /// <summary>
    /// 小さい方のﾌﾟﾛｸﾞﾚｽﾊﾞーをｳﾈｳﾈ
    /// </summary>
    [ObservableProperty]
    public partial bool SubProgressWasIndeterminate { get; set; } = false;

    /// <summary>
    /// ｲﾝｸﾘﾒﾝﾀﾙ検索有効？
    /// </summary>
    [ObservableProperty]
    public partial bool IncrementalSearch { get; set; } = true;

    /// <summary>
    /// ﾄﾚｲにぶち込むﾓーﾄﾞ
    /// </summary>
    [ObservableProperty]
    public partial bool MinimizeToTray { get; set; } = false;

    /// <summary>
    /// 詳細ﾍﾟｲﾝ表示切り替え
    /// </summary>
    [ObservableProperty]
    public partial bool ShowDetailPane { get; set; } = false;

    /// <summary>
    /// 詳細ﾍﾟｲﾝのﾌﾟﾚﾋﾞｭーに元画像使う？
    /// </summary>
    [ObservableProperty]
    public partial bool DetailPaneShowOriginal { get; set; } = false;

    /// <summary>
    /// 詳細ﾍﾟｲﾝの幅
    /// </summary>
    [ObservableProperty]
    public partial double DetailPaneWidth { get; set; } = 128;

    /// <summary>
    /// 詳細ﾍﾟｲﾝの幅(保存用)
    /// </summary>
    [ObservableProperty]
    public partial double DetailPaneActualWidth { get; set; } = 128;

    /// <summary>
    /// 詳細(ryのｻﾑﾈｻｲｽﾞ
    /// </summary>
    [ObservableProperty]
    public partial double DetailThumbnailSize { get; set; } = 128;

    /// <summary>
    /// 詳(ryのﾒﾀﾃﾞーﾀﾑ表示欄
    /// </summary>
    public ObservableCollection<MetadataPair> DetailFields { get; set; } = new();

    /// <summary>
    /// 詳(ry表示切り替え
    /// </summary>
    [RelayCommand]
    void SwitchDetailPane() {
        ShowDetailPane = !ShowDetailPane;
    }

    /// <summary>
    /// ﾆﾗｸｼｮﾝ有効？
    /// </summary>
    [ObservableProperty]
    public partial bool NiractionEnabled { get; set; } = false;

    /// <summary>
    /// ｲﾓｹﾞｲﾝﾃﾞｯｸｽ起動
    /// </summary>
    [RelayCommand]
    void LaunchIndexTool() {
        OnLaunchIndexTool?.Invoke();
    }
    public event Action? OnLaunchIndexTool;

    /// <summary>
    /// ﾃﾞーﾀﾑ再読み込み
    /// </summary>
    [RelayCommand]
    void ReloadDatum() {
        if (IsProfileSwitchable) {
            OnReloadDatum?.Invoke();
        }
    }
    public event Action? OnReloadDatum;

    /// <summary>
    /// 詳細ﾍﾟｲﾝのｸﾘｱ
    /// </summary>
    [RelayCommand]
    void ClearDetailPane()
    {
        OnClearDetailPane?.Invoke();
    }
    public event Action? OnClearDetailPane;

    /// <summary>
    /// ⚠️ 緊急事態 ⚠️
    /// </summary>
    [RelayCommand]
    void Emergency() {
        ShowDetailPane = false;
        ClearDetailPane();
        SetDisplayMode(MurDisplayPreset.List);
    }

    /// <summary>
    /// ｻｰﾁﾓにﾌｫーｶｽを当てる
    /// </summary>
    [RelayCommand]
    void FocusSearchTextBox()
    {
        OnFocusSearchTextBox?.Invoke();
    }
    public event Action? OnFocusSearchTextBox;


    /// <summary>
    /// ﾘｽﾄ表示ﾓーﾄﾞ切替
    /// </summary>
    /// <param name="mode"></param>
    [RelayCommand]
    void SetDisplayMode(MurDisplayPreset preset) {
        OnSetDisplayMode?.Invoke(preset);
    }
    public event Action<MurDisplayPreset>? OnSetDisplayMode;

    /// <summary>
    /// ｱﾊﾞｳﾂを開く
    /// </summary>
    [RelayCommand]
    void OpenAboutDialog()
    {
        OnOpenAboutDialog?.Invoke();
    }
    public event Action? OnOpenAboutDialog;

    /// <summary>
    /// ﾌｧｲﾙ開く
    /// </summary>
    public KetCommand<FileItem> OpenFileCommand { get; set; } = new();

    /// <summary>
    /// ﾌｫﾙﾀﾞー開く
    /// </summary>
    public KetCommand<FileItem> OpenFolderCommand { get; set; } = new();


    /// <summary>
    /// ﾊﾟｽのｺﾋﾟｨ
    /// </summary>
    /// <param name="parameter"></param>
    [RelayCommand]
    private void CopyFilePath(object? parameter) {
        if (parameter is FileItem eitem) {
            try {
                SetClipboardText(eitem.FullName.AutoQuotePath());
            }
            catch {
                // いや、どうしようもねえよ
            }
        }
    }

    /// <summary>
    /// ｸﾘｯﾌﾟﾎﾞーﾄﾞに文字列を入れる
    /// </summary>
    /// <param name="text"></param>
    protected abstract void SetClipboardText(string text);

    /// <summary>
    /// 類似検索
    /// </summary>
    /// <param name="parameter"></param>
    [RelayCommand(CanExecute = nameof(CanSearchSimilarImages))]
    private void SearchSimilarImages(object? parameter)
    {
        // 切断ﾏーｶーで落ちる対策
        if (IsProfileSwitchable && parameter is FileItem { HasImageHashes: true } item) {
            OnSearchSimilarImages?.Invoke(item);
        }
    }

    /// <summary>
    /// 類似検索？
    /// </summary>
    /// <param name="parameter"></param>
    /// <returns></returns>
    private bool CanSearchSimilarImages(object? parameter)
        => IsProfileSwitchable && parameter is FileItem { HasImageHashes: true };

    public event Action<FileItem>? OnSearchSimilarImages;

    /// <summary>
    /// 類似検索に使うﾊｯｼｭの組み合わせ
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(UsePHash), nameof(UseAHash), nameof(UseDHash), nameof(UseWHash))]
    [NotifyCanExecuteChangedFor(nameof(ToggleSimilarHashCommand))]
    public partial SimilarHashAlgorithm SimilarHashes { get; set; } = SimilarHashAlgorithm.All;
    partial void OnSimilarHashesChanged(SimilarHashAlgorithm value)
    {
        OnSimilarHashSwitched?.Invoke(value);
    }
    public event Action<SimilarHashAlgorithm>? OnSimilarHashSwitched;

    public bool UsePHash => SimilarHashes.HasFlag(SimilarHashAlgorithm.PHash);
    public bool UseAHash => SimilarHashes.HasFlag(SimilarHashAlgorithm.AHash);
    public bool UseDHash => SimilarHashes.HasFlag(SimilarHashAlgorithm.DHash);
    public bool UseWHash => SimilarHashes.HasFlag(SimilarHashAlgorithm.WHash);

    /// <summary>
    /// 類似検索に使うﾊｯｼｭを切り替える
    /// </summary>
    /// <param name="hash"></param>
    [RelayCommand(CanExecute = nameof(CanToggleSimilarHash))]
    private void ToggleSimilarHash(SimilarHashAlgorithm hash)
    {
        if (CanToggleSimilarHash(hash)) {
            SimilarHashes ^= hash;
        }
    }

    /// <summary>
    /// 最後の1種は外させない
    /// </summary>
    /// <param name="hash"></param>
    /// <returns></returns>
    private bool CanToggleSimilarHash(SimilarHashAlgorithm hash)
        => ((SimilarHashes ^ hash) & SimilarHashAlgorithm.All) != SimilarHashAlgorithm.None;

    /// <summary>
    /// 設定ﾌｫﾙﾀﾞーを開く
    /// </summary>
    [RelayCommand]
    private void OpenSettingFolder() => OnOpenSettingFolder?.Invoke();

    public event Action? OnOpenSettingFolder;

    /// <summary>
    /// ﾌﾟﾛﾌｧｲﾙﾌｫﾙﾀﾞーを開く
    /// </summary>
    [RelayCommand]
    private void OpenProfileFolder() => OnOpenProfileFolder?.Invoke();
    public event Action? OnOpenProfileFolder;

    /// <summary>
    /// ﾗﾍﾞﾙ編集を開く
    /// </summary>
    [RelayCommand]
    private void OpenLabelEdit() => OnOpenLabelEdit?.Invoke();
    public event Action? OnOpenLabelEdit;

    /// <summary>
    /// ﾆﾗｸｼｮﾝにVerify送信
    /// </summary>
    /// <param name="parameter"></param>
    /// <returns></returns>
    [RelayCommand]
    Task SendNiractionVerify(object? parameter) =>
        SendNiraction(parameter, (nir, fr) => nir.Verify(fr, CancellationToken.None));

    /// <summary>
    /// ﾆﾗｸｼｮﾝにDistance送信
    /// </summary>
    /// <param name="parameter"></param>
    /// <returns></returns>
    [RelayCommand]
    Task SendNiractionDistance(object? parameter) =>
        SendNiraction(parameter, (nir, fr) => nir.Distance(fr, CancellationToken.None));

    /// <summary>
    /// ﾆﾗｸｼｮﾝ送信の共通部分
    /// </summary>
    /// <param name="parameter"></param>
    /// <param name="send"></param>
    /// <returns></returns>
    async Task SendNiraction(object? parameter, Func<NiractionClient, FileRecord, Task<NiractionResponse?>> send)
    {
        // 切断ﾏーｶーで落ちる対策
        if (!NiractionEnabled || parameter is not FileItem item) {
            return;
        }
        try {
            // DB読み込みはUIｽﾚｯﾄﾞの外で
            var fr = await Task.Run(() => IndexStore.Instance.GetFileRecord(item.Id, CancellationToken.None));
            if (fr == null) {
                return;
            }
            var nir = new NiractionClient(Profile.Active.Niraction);
            var resp = await send(nir, fr);
            OnNiractionResponse?.Invoke(resp, null);
        }
        catch (Exception r) {
            OnNiractionResponse?.Invoke(null, r.Message);
        }
    }
    public event Action<NiractionResponse?, string?>? OnNiractionResponse;
}
