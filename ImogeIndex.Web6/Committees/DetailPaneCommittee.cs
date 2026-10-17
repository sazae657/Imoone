using ImogeIndex.Web6.Dictators;
using ImogeIndex.Web6.Modelle;
using ImogeIndex.Web6.Storage;
using ImogeIndex.Mur;

namespace ImogeIndex.Web6.Committees;

/// <summary>
/// 詳細ﾍﾟｲﾝとｻﾑﾈのﾌﾟﾚﾋﾞｭーを統治する
/// </summary>
/// <typeparam name="TBitmap">表示用の画像型</typeparam>
public sealed class DetailPaneCommittee<TBitmap> where TBitmap : class
{
    readonly ImooneModell model;
    readonly Action<TBitmap?> showBitmapDelegaty;
    readonly Func<double> dpiScaleDelegaty;
    readonly Func<double> screenLongSideDelegaty;
    readonly CancellationToken closingToken;
    readonly ImagePreviewDictator<TBitmap> imagePreviewDictator;
    readonly DetailPaneDictator detailPaneDictator;
    CancellationTokenSource? detailImageTokenSource;

    /// <summary>
    /// </summary>
    /// <param name="modell">ﾓﾃﾞﾚ</param>
    /// <param name="texts">表示文言</param>
    /// <param name="decoder">ﾌﾟﾚﾋﾞｭー画像のﾃﾞｺーﾀﾞー</param>
    /// <param name="showDetailBitmap">詳細ﾍﾟｲﾝに画像を出す</param>
    /// <param name="dpiScaleX">表示倍率</param>
    /// <param name="screenLongSideDip">画面の長辺(DIP)</param>
    /// <param name="closing"></param>
    public DetailPaneCommittee(ImooneModell modell, IImoonePalabras texts, IImagePreviewDecoder<TBitmap> decoder,
        Action<TBitmap?> showDetailBitmap, Func<double> dpiScaleX, Func<double> screenLongSideDip, CancellationToken closing)
    {
        model = modell;
        showBitmapDelegaty = showDetailBitmap;
        dpiScaleDelegaty = dpiScaleX;
        screenLongSideDelegaty = screenLongSideDip;
        closingToken = closing;
        imagePreviewDictator = new(decoder);
        detailPaneDictator = new(texts);
    }

    /// <summary>
    /// 事前作成ｻﾑﾈが無い時に実物を縮めて読む寸法
    /// </summary>
    private int PreviewDecodeSize => (int)Math.Ceiling(
        Math.Max(128, model.DetailThumbnailSize) * dpiScaleDelegaty());

    /// <summary>
    /// 詳細ﾍﾟｲﾝで実物を出す時の長辺上限
    /// </summary>
    private int OriginalDecodeSize => (int)Math.Ceiling(screenLongSideDelegaty() * dpiScaleDelegaty());

    /// <summary>
    /// 詳細ﾍﾟｲﾝに項目を出す
    /// </summary>
    /// <param name="fileItem"></param>
    public void Show(FileItem fileItem)
    {
        Cancel();
        showBitmapDelegaty(model.DetailPaneShowOriginal ? null : imagePreviewDictator.GetCachedThumbnail(fileItem));
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
        showBitmapDelegaty(null);
        model.DetailFields.Clear();
    }

    /// <summary>
    /// 詳細ﾍﾟｲﾝとｻﾑﾈｷｬｯｼｭを空にする(ﾃﾞーﾀﾑ差し替え時)
    /// </summary>
    public void Reset()
    {
        Clear();
        imagePreviewDictator.Clear();
    }

    /// <summary>
    /// ｻﾑﾈを非同期で詰める
    /// </summary>
    /// <param name="fileItem"></param>
    /// <param name="applyDelegaty">詰める</param>
    /// <param name="isStillDelegaty">詰める先がまだこの項目を表示してる？</param>
    /// <returns></returns>
    public async Task FillThumbnailAsync(FileItem fileItem, Action<TBitmap?> applyDelegaty, Func<bool> isStillDelegaty)
    {
        // 読み込み中にｺﾝﾃﾅが別の項目に再利用されてたら捨てる
        var cached = imagePreviewDictator.GetCachedThumbnail(fileItem);
        applyDelegaty(cached);
        if (cached != null) {
            return;
        }
        var bitmap = await imagePreviewDictator.GetThumbnailAsync(fileItem, PreviewDecodeSize, closingToken);
        if (!closingToken.IsCancellationRequested && isStillDelegaty()) {
            applyDelegaty(bitmap);
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
                showBitmapDelegaty(bitmap);
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
