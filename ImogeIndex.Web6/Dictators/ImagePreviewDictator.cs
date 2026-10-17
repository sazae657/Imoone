using ImogeIndex.Mur;
using ImogeIndex.Storage;

namespace ImogeIndex.Web6.Dictators;

/// <summary>
/// ﾌﾟﾚﾋﾞｭー画像のﾃﾞｺーﾀﾞー
/// </summary>
/// <typeparam name="TBitmap">表示用の画像型</typeparam>
public interface IImagePreviewDecoder<TBitmap> where TBitmap : class
{
    /// <summary>
    /// 事前作成ｻﾑﾈのｽﾄﾘーﾑから画像を作る(ﾜーｶーｽﾚｯﾄﾞから呼ばれる)
    /// </summary>
    /// <param name="stream"></param>
    /// <returns></returns>
    TBitmap? DecodeThumbnail(Stream stream);

    /// <summary>
    /// 実物を読む(ﾜーｶーｽﾚｯﾄﾞから呼ばれる、回転情報で正立させること)
    /// </summary>
    /// <param name="fileItem"></param>
    /// <param name="maxSize">長辺の上限(これより大きい画像は縮めてﾃﾞｺーﾄﾞ、0なら原寸)</param>
    /// <param name="cancellationToken"></param>
    /// <returns>読めなければnull</returns>
    TBitmap? DecodeImage(FileItem fileItem, int maxSize, CancellationToken cancellationToken);
}

/// <summary>
/// ﾌﾟﾚﾋﾞｭー
/// </summary>
/// <typeparam name="TBitmap">表示用の画像型</typeparam>
/// <param name="decoder">ﾃﾞｺーﾀﾞー</param>
public sealed class ImagePreviewDictator<TBitmap>(IImagePreviewDecoder<TBitmap> decoder) where TBitmap : class
{
    const int MaxThumbnailCache = 64;
    const int MaxPreviewCache = 8;
    readonly Dictionary<long, TBitmap> thumbnailCache = new();
    readonly Queue<long> thumbnailOrder = new();
    readonly Dictionary<long, TBitmap> previewCache = new();
    readonly Queue<long> previewOrder = new();
    int cachenation;

    // ｻﾑﾈ用ｽﾄｱの接続はｽﾚｯﾄﾞｾーﾌじゃないのでｼﾘｱﾙ読み込み
    readonly SemaphoreSlim thumbnailLock = new(1, 1);

    /// <summary>
    /// ｷｬｯｼｭ済みのｻﾑﾈ(無ければnull)
    /// </summary>
    /// <param name="fileItem"></param>
    /// <returns></returns>
    public TBitmap? GetCachedThumbnail(FileItem fileItem)
        => thumbnailCache.TryGetValue(fileItem.Id, out var cachedBitmap) ? cachedBitmap : null;

    /// <summary>
    /// 事前作成ｻﾑﾈ取得
    /// UIｽﾚｯﾄﾞから呼ぶこと(ｷｬｯｼｭはUIｽﾚｯﾄﾞでしか触らない)
    /// </summary>
    /// <param name="fileItem"></param>
    /// <param name="fallbackSize">事前作成ｻﾑﾈが無くて実物を読む時の長辺</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task<TBitmap?> GetThumbnailAsync(
        FileItem fileItem,
        int fallbackSize,
        CancellationToken cancellationToken)
    {
        var nation = cachenation;
        if (thumbnailCache.TryGetValue(fileItem.Id, out var cachedBitmap))
        {
            return cachedBitmap;
        }

        try
        {
            var bitmap = await Task.Run(async () => {
                await thumbnailLock.WaitAsync(cancellationToken).ConfigureAwait(false);
                try {
                    cancellationToken.ThrowIfCancellationRequested();
                    return LoadThumbnail(fileItem.Id);
                }
                finally {
                    thumbnailLock.Release();
                }
            }, cancellationToken);
            if (nation != cachenation) {
                return null;
            }
            // 実物を読む(でかいのでｻﾑﾈ寸法に縮めてﾃﾞｺーﾄﾞ)
            bitmap ??= await Task.Run(() => decoder.DecodeImage(fileItem, fallbackSize, cancellationToken), cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (nation != cachenation) {
                return null;
            }
            if (bitmap != null && !thumbnailCache.ContainsKey(fileItem.Id)) {
                AddToCache(thumbnailCache, thumbnailOrder, fileItem.Id, bitmap, MaxThumbnailCache);
            }
            return bitmap;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// ｻﾑﾈ取得
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    private TBitmap? LoadThumbnail(long id) {
        if (!ThumbnailStore.IsOpened) {
            return null;
        }
        using var stream = ThumbnailStore.Instance.GetStream(id);
        if (stream == null) {
            return null;
        }
        return decoder.DecodeThumbnail(stream);
    }

    /// <summary>
    /// 詳細ﾍﾟｲﾝ用画像取得
    /// </summary>
    /// <param name="fileItem"></param>
    /// <param name="maxSize">長辺の上限(これより大きい画像は縮めてﾃﾞｺーﾄﾞ、0なら原寸)</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task<TBitmap?> GetPreviewAsync(
        FileItem fileItem,
        int maxSize,
        CancellationToken cancellationToken)
    {
        var generation = cachenation;
        if (previewCache.TryGetValue(fileItem.Id, out var cachedBitmap))
        {
            return cachedBitmap;
        }

        try
        {
            var bitmap = await Task.Run(() => decoder.DecodeImage(fileItem, maxSize, cancellationToken), cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (generation != cachenation) {
                return null;
            }
            if (bitmap != null && !previewCache.ContainsKey(fileItem.Id))
            {
                AddToCache(previewCache, previewOrder, fileItem.Id, bitmap, MaxPreviewCache);
            }
            return bitmap;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return null;
        }
    }

    /// <summary>
    /// ｷｬｯｼｭｸﾘｱ
    /// </summary>
    public void Clear()
    {
        cachenation++;
        thumbnailCache.Clear();
        thumbnailOrder.Clear();
        previewCache.Clear();
        previewOrder.Clear();
    }

    /// <summary>
    /// ｷｬｯｼｭ
    /// </summary>
    /// <param name="cache"></param>
    /// <param name="order"></param>
    /// <param name="id"></param>
    /// <param name="bitmap"></param>
    /// <param name="limit"></param>
    private static void AddToCache(
        Dictionary<long, TBitmap> cache,
        Queue<long> order,
        long id,
        TBitmap bitmap,
        int limit)
    {
        while (order.Count >= limit)
        {
            cache.Remove(order.Dequeue());
        }
        cache[id] = bitmap;
        order.Enqueue(id);
    }
}
