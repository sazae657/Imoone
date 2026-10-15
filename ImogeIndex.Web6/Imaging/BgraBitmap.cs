using SkiaSharp;

namespace ImogeIndex.Web6.Imaging;

/// <summary>
/// 乗算済みBGRAの画素(UIｽﾚｯﾄﾞの外で作って、表示用の画像型への変換だけUIｽﾚｯﾄﾞでやる用)
/// </summary>
/// <param name="Width">幅</param>
/// <param name="Height">高さ</param>
/// <param name="Pixels">画素(行間の詰め物なし)</param>
public sealed record BgraBitmap(int Width, int Height, byte[] Pixels)
{
    /// <summary>
    /// ｷｬｯｼｭの重さ(ﾊﾞｲﾄ)
    /// </summary>
    public long Bytes => Pixels.LongLength;

    /// <summary>
    /// SKBitmapから作る
    /// </summary>
    /// <param name="source"></param>
    /// <param name="maxEdge">長辺の上限(これより大きければ縮める、0なら原寸)</param>
    /// <returns></returns>
    public static BgraBitmap From(SKBitmap source, int maxEdge = 0)
    {
        var width = source.Width;
        var height = source.Height;
        var longEdge = Math.Max(width, height);
        if (maxEdge > 0 && longEdge > maxEdge) {
            var scale = (double)maxEdge / longEdge;
            width = Math.Max(1, (int)Math.Round(width * scale));
            height = Math.Max(1, (int)Math.Round(height * scale));
        }
        var info = new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
        using var target = new SKBitmap(info, info.Width * info.BytesPerPixel);
        if (!source.ScalePixels(target, new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear))) {
            throw new InvalidOperationException(nameof(SKBitmap.ScalePixels));
        }
        return new BgraBitmap(width, height, target.Bytes);
    }
}
