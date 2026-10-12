using SkiaSharp;

namespace ImogeIndex.Imoging;

/// <summary>
/// 保存用ｻﾑﾈｲﾙの縮小と形式選択を行う。
/// </summary>
public static class ThumbnailEncoder
{
    private const int JpegQuality = 85;


    /// <summary>
    /// ｻﾑﾈを作って不透明画像はJPEG、透過画像はPNGで返す
    /// </summary>
    /// <param name="bitmap"></param>
    /// <param name="maxEdgeLength"></param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    public static byte[] Encode(SKBitmap bitmap, int maxEdgeLength)
    {
        ArgumentNullException.ThrowIfNull(bitmap);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxEdgeLength);

        using var resized = Math.Max(bitmap.Width, bitmap.Height) > maxEdgeLength
            ? bitmap.ResizeByMaxEdge(maxEdgeLength)
            : null;
        var thumbnail = resized ?? bitmap;
        var format = IsOpaque(thumbnail) ? SKEncodedImageFormat.Jpeg : SKEncodedImageFormat.Png;
        using var data = thumbnail.Encode(format, format == SKEncodedImageFormat.Jpeg ? JpegQuality : 100)
            ?? throw new InvalidOperationException(Properties.Resources.IM_E_EncodeThumbnailFailed);
        return data.ToArray();
    }

    /// <summary>
    /// 透過？
    /// </summary>
    /// <param name="bitmap"></param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    private static bool IsOpaque(SKBitmap bitmap)
    {
        if (bitmap.AlphaType == SKAlphaType.Opaque) {
            return true;
        }

        // α付きでも全画素が不透明ならJPEGを使う(PNGでｻﾑﾈDBがｸｿﾃﾞｶくなる対策)
        using var pixels = bitmap.PeekPixels()
            ?? throw new InvalidOperationException(Properties.Resources.IM_E_GetThumbnailPixelsFailed);
        return pixels.ComputeIsOpaque();
    }
}
