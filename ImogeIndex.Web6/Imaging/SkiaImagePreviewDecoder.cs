using ImogeIndex.Imoging;
using ImogeIndex.Web6.Dictators;
using ImogeIndex.Mur;
using ImogeIndex.Sources;

namespace ImogeIndex.Web6.Imaging;

/// <summary>
/// ﾌﾟﾚﾋﾞｭーのSkia版ﾃﾞｺーﾀﾞー
/// 回転補正とImageMagickへのﾌｫーﾙﾊﾞｯｸはImageLoaderに任せる
/// </summary>
public sealed class SkiaImagePreviewDecoder : IImagePreviewDecoder<BgraBitmap>
{
    public static SkiaImagePreviewDecoder Instance { get; } = new();

    /// <summary>
    /// 事前作成ｻﾑﾈ
    /// </summary>
    /// <param name="stream"></param>
    /// <returns></returns>
    public BgraBitmap? DecodeThumbnail(Stream stream)
    {
        using var bitmap = ImageLoader.Load(stream);
        return BgraBitmap.From(bitmap);
    }

    /// <summary>
    /// 実物を読む
    /// </summary>
    /// <param name="fileItem"></param>
    /// <param name="maxSize">長辺の上限(これより大きい画像は縮めてﾃﾞｺーﾄﾞ、0なら原寸)</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public BgraBitmap? DecodeImage(FileItem fileItem, int maxSize, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try {
            var bytes = FileSourceRegistry.Instance.ReadAllBytes(fileItem.FullName);
            cancellationToken.ThrowIfCancellationRequested();
            using var bitmap = ImageLoader.Load(bytes, maxSize);
            return BgraBitmap.From(bitmap, maxSize);
        }
        catch {
            cancellationToken.ThrowIfCancellationRequested();
            return null;
        }
    }
}
