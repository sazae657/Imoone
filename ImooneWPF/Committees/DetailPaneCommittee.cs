using ImogeIndex.Mur;
using ImooneWPF.Dictators;
using ImooneWPF.Modelle;
using ImooneWPF.Storage;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ImooneWPF.Committees;

/// <summary>
/// 詳細ﾍﾟｲﾝとｻﾑﾈのﾌﾟﾚﾋﾞｭーを統治する
/// 選択が変わるたびに前の読み込みを捨てて、画像とﾈﾀを早く終わった方から出す
/// </summary>
internal sealed class DetailPaneCommittee
{
    readonly MainWindowModell model;
    readonly Visual dpiOwner;
    readonly CancellationToken closingToken;
    readonly ImagePreviewDictator imagePreviewDictator = new();
    readonly DetailPaneDictator detailPaneDictator = new();
    CancellationTokenSource? detailImageTokenSource;

    /// <summary>
    /// </summary>
    /// <param name="modell"></param>
    /// <param name="dpiSource"></param>
    /// <param name="closing"></param>
    public DetailPaneCommittee(MainWindowModell modell, Visual dpiSource, CancellationToken closing)
    {
        model = modell;
        dpiOwner = dpiSource;
        closingToken = closing;
    }

    /// <summary>
    /// 事前作成ｻﾑﾈが無い時に実物を縮めて読む寸法
    /// </summary>
    private int PreviewDecodeSize => (int)Math.Ceiling(
        Math.Max(128, model.DetailThumbnailSize) * VisualTreeHelper.GetDpi(dpiOwner).DpiScaleX);

    /// <summary>
    /// 詳細ﾍﾟｲﾝで実物を出す時の長辺上限
    /// ﾍﾟｲﾝが画面より大きくなることはないので、8Kとかを原寸で抱えないよう画面の長辺で頭打ちにする
    /// </summary>
    private int OriginalDecodeSize => (int)Math.Ceiling(
        Math.Max(SystemParameters.PrimaryScreenWidth, SystemParameters.PrimaryScreenHeight)
        * VisualTreeHelper.GetDpi(dpiOwner).DpiScaleX);

    /// <summary>
    /// 詳細ﾍﾟｲﾝに項目を出す
    /// </summary>
    /// <param name="fileItem"></param>
    public void Show(FileItem fileItem)
    {
        Cancel();
        model.DetailBitmap = model.DetailPaneShowOriginal ? null : imagePreviewDictator.GetCachedThumbnail(fileItem);
        model.DetailFields.Clear();
        detailImageTokenSource = CancellationTokenSource.CreateLinkedTokenSource(closingToken);
        _ = LoadDetailAsync(fileItem, detailImageTokenSource.Token);
    }

    /// <summary>
    /// 詳細ﾍﾟｲﾝの読み込み中止
    /// </summary>
    public void Cancel()
    {
        detailImageTokenSource?.Cancel();
        detailImageTokenSource?.Dispose();
        detailImageTokenSource = null;
    }

    /// <summary>
    /// 詳細ﾍﾟｲﾝの内容ｸﾘｱ
    /// </summary>
    public void Clear()
    {
        Cancel();
        model.DetailBitmap = null;
        model.DetailFields.Clear();
    }

    /// <summary>
    /// 詳細ﾍﾟｲﾝもｻﾑﾈｷｬｯｼｭも空にする(ﾃﾞーﾀﾑ差し替え時)
    /// </summary>
    public void Reset()
    {
        Clear();
        imagePreviewDictator.Clear();
    }

    /// <summary>
    /// ｻﾑﾈを非同期で詰める
    /// 読み込み中にｺﾝﾃﾅが別の項目に再利用されてたら捨てる
    /// </summary>
    /// <param name="image"></param>
    /// <param name="owner"></param>
    /// <param name="fileItem"></param>
    /// <returns></returns>
    public async Task FillThumbnailAsync(Image image, FrameworkElement owner, FileItem fileItem)
    {
        var cached = imagePreviewDictator.GetCachedThumbnail(fileItem);
        image.Source = cached;
        if (cached != null) {
            return;
        }
        var bitmap = await imagePreviewDictator.GetThumbnailAsync(fileItem, PreviewDecodeSize, closingToken);
        if (!closingToken.IsCancellationRequested && ReferenceEquals(owner.DataContext, fileItem)) {
            image.Source = bitmap;
        }
    }

    /// <summary>
    /// 詳細ﾍﾟｲﾝの画像とﾈﾀをｽﾚﾌﾟで読み込む
    /// </summary>
    /// <param name="fileItem"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    private async Task LoadDetailAsync(FileItem fileItem, CancellationToken cancellationToken)
    {
        // 画像とﾈﾀは早く終わった方から出す
        await Task.WhenAll(LoadBitmapAsync(), LoadFieldsAsync());

        async Task LoadBitmapAsync()
        {
            var bitmap = model.DetailPaneShowOriginal
                ? await imagePreviewDictator.GetPreviewAsync(fileItem, OriginalDecodeSize, cancellationToken)
                : await imagePreviewDictator.GetThumbnailAsync(fileItem, PreviewDecodeSize, cancellationToken);
            if (!cancellationToken.IsCancellationRequested) {
                model.DetailBitmap = bitmap;
            }
        }

        async Task LoadFieldsAsync()
        {
            IReadOnlyList<MetadataPair> fields;
            try {
                fields = await detailPaneDictator.LoadMetadataAsync(fileItem, cancellationToken);
            }
            catch (Exception) {
                // 選択が変わった or 読み込み中にﾃﾞーﾀﾑが閉じられた
                return;
            }
            if (cancellationToken.IsCancellationRequested) {
                return;
            }
            model.DetailFields.Clear();
            foreach (var field in fields) {
                model.DetailFields.Add(field);
            }
        }
    }
}
