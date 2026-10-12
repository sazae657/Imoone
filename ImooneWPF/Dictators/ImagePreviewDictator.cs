using ImageMagick;
using ImogeIndex.Imoging;
using ImogeIndex.Mur;
using ImogeIndex.Storage;
using SkiaSharp;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ImooneWPF.Dictators;

/// <summary>
/// 画像ﾌﾟﾚﾋﾞｭー
/// </summary>
internal sealed class ImagePreviewDictator
{
    const int MaxThumbnailCache = 64;
    const int MaxPreviewCache = 8;
    readonly Dictionary<long, BitmapSource> thumbnailCache = new();
    readonly Queue<long> thumbnailOrder = new();
    readonly Dictionary<long, BitmapSource> previewCache = new();
    readonly Queue<long> previewOrder = new();
    int cachenation;

    // ｻﾑﾈ用ｽﾄｱの接続はｽﾚｯﾄﾞｾーﾌじゃないのでｼﾘｱﾙ読み込み
    readonly SemaphoreSlim thumbnailLock = new(1, 1);

    /// <summary>
    /// ｷｬｯｼｭ済みのｻﾑﾈ(無ければnull)
    /// </summary>
    /// <param name="fileItem"></param>
    /// <returns></returns>
    public BitmapSource? GetCachedThumbnail(FileItem fileItem)
        => thumbnailCache.TryGetValue(fileItem.Id, out var cachedBitmap) ? cachedBitmap : null;

    /// <summary>
    /// 事前作成ｻﾑﾈ取得
    /// UIｽﾚｯﾄﾞから呼ぶこと(ｷｬｯｼｭはUIｽﾚｯﾄﾞでしか触らない)
    /// </summary>
    /// <param name="fileItem"></param>
    /// <param name="fallbackSize">事前作成ｻﾑﾈが無くて実物を読む時の長辺</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task<BitmapSource?> GetThumbnailAsync(
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
            bitmap ??= await Task.Run(() => LoadImage(fileItem, fallbackSize, cancellationToken), cancellationToken);
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
    public BitmapSource? LoadThumbnail(long id) {
        if (!ThumbnailStore.IsOpened) {
            return null;
        }
        using var stream = ThumbnailStore.Instance.GetStream(id);
        if (stream == null) {
            return null;
        }
        var betmap = new BitmapImage();
        betmap.BeginInit();
        betmap.StreamSource = stream;
        betmap.CacheOption = BitmapCacheOption.OnLoad;
        betmap.EndInit();
        betmap.Freeze();
        return betmap;
    }

    /// <summary>
    /// 詳細ﾍﾟｲﾝ用画像取得
    /// </summary>
    /// <param name="fileItem"></param>
    /// <param name="maxSize">長辺の上限(これより大きい画像は縮めてﾃﾞｺーﾄﾞ、0なら原寸)</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task<BitmapSource?> GetPreviewAsync(
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
            var bitmap = await Task.Run(() => LoadImage(fileItem, maxSize, cancellationToken), cancellationToken);
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
    /// 画像を読み込む(WPFでﾀﾞﾒならMagickImage)
    /// </summary>
    /// <param name="fileItem"></param>
    /// <param name="maxSize">長辺の上限(これより大きい画像は縮めてﾃﾞｺーﾄﾞ、0なら原寸)</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    private static BitmapSource? LoadImage(FileItem fileItem, int maxSize, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path = fileItem.FullName;
        try
        {
            // WPFは回転情報を無視するので自前で当てる
            var origin = ImageLoader.ReadOrigin(path);
            var betmap = new BitmapImage();
            betmap.BeginInit();
            betmap.UriSource = new Uri(path, UriKind.Absolute);
            betmap.CacheOption = BitmapCacheOption.OnLoad;
            if (maxSize > 0 && fileItem.Width > 0 && fileItem.Height > 0 &&
                Math.Max(fileItem.Width, fileItem.Height) > maxSize) {
                if ((fileItem.Height > fileItem.Width) != ImageLoader.SwapsAxes(origin)) {
                    betmap.DecodePixelHeight = maxSize;
                }
                else {
                    betmap.DecodePixelWidth = maxSize;
                }
            }
            betmap.EndInit();
            betmap.Freeze();
            return Orient(betmap, origin);
        }
        catch
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                using var image = new MagickImage(path);
                // 回転情報で正立させる
                image.AutoOrient();
                if (maxSize > 0) {
                    // 縮小のみ
                    image.Thumbnail(new MagickGeometry($"{maxSize}x{maxSize}>"));
                }
                var betmap = image.ToBitmapSource();
                betmap.Freeze();
                return betmap;
            }
            catch
            {
                return null;
            }
        }
    }

    /// <summary>
    /// 回転情報に従って正立させる
    /// </summary>
    /// <param name="betmap"></param>
    /// <param name="origin"></param>
    /// <returns></returns>
    private static BitmapSource Orient(BitmapSource betmap, SKEncodedOrigin origin)
    {
        // TransformGroupは子を先頭から順に適用(回転は時計回り)
        Transform? transform = origin switch {
            SKEncodedOrigin.TopRight => new ScaleTransform(-1, 1),
            SKEncodedOrigin.BottomRight => new RotateTransform(180),
            SKEncodedOrigin.BottomLeft => new ScaleTransform(1, -1),
            SKEncodedOrigin.LeftTop => new TransformGroup { Children = { new RotateTransform(90), new ScaleTransform(-1, 1) } },
            SKEncodedOrigin.RightTop => new RotateTransform(90),
            SKEncodedOrigin.RightBottom => new TransformGroup { Children = { new RotateTransform(270), new ScaleTransform(-1, 1) } },
            SKEncodedOrigin.LeftBottom => new RotateTransform(270),
            _ => null,
        };
        if (transform == null) {
            return betmap;
        }
        var result = new TransformedBitmap(betmap, transform);
        result.Freeze();
        return result;
    }

    /// <summary>
    /// ｷｬｯｼｭ
    /// </summary>
    /// <param name="cache"></param>
    /// <param name="order"></param>
    /// <param name="id"></param>
    /// <param name="bitmap"></param>
    private static void AddToCache(
        Dictionary<long, BitmapSource> cache,
        Queue<long> order,
        long id,
        BitmapSource bitmap,
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
