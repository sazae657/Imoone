using ImogeIndex.Web6.Dictators;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ImooneWPF.Dictators;

/// <summary>
/// 一覧ｻﾑﾈのWPF用ﾃﾞｺーﾀﾞー
/// </summary>
internal sealed class WpfTileBitmapDecoder : ITileBitmapDecoder<BitmapSource>
{
    public static WpfTileBitmapDecoder Instance { get; } = new();

    /// <summary>
    /// BLOBから画像ﾛーﾄﾞ
    /// </summary>
    /// <param name="bytes"></param>
    /// <param name="size"></param>
    /// <param name="portrait"></param>
    /// <returns></returns>
    public BitmapSource? Decode(byte[] bytes, int size, bool portrait)
    {
        using var stream = new MemoryStream(bytes, false);
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        if (portrait) {
            bitmap.DecodePixelHeight = size;
        }
        else {
            bitmap.DecodePixelWidth = size;
        }
        bitmap.StreamSource = stream;
        bitmap.EndInit();

        // 長辺を上限内に収める
        var scale = Math.Min(1.0, (double)size / Math.Max(bitmap.PixelWidth, bitmap.PixelHeight));
        if (scale >= 1 && (bitmap.Format == PixelFormats.Bgr32 || bitmap.Format == PixelFormats.Pbgra32)) {
            // そのまま描ける形式なら変換とｺﾋﾟーを省く(JPEGはだいたいBgr32で来る)
            bitmap.Freeze();
            return bitmap;
        }
        BitmapSource pixels = bitmap;
        if (scale < 1) {
            pixels = new TransformedBitmap(pixels, new ScaleTransform(scale, scale));
        }
        pixels = new FormatConvertedBitmap(pixels, PixelFormats.Pbgra32, null, 0);
        var stride = pixels.PixelWidth * 4;
        var buffer = new byte[stride * pixels.PixelHeight];
        pixels.CopyPixels(buffer, stride, 0);
        var result = BitmapSource.Create(pixels.PixelWidth, pixels.PixelHeight, 96, 96, PixelFormats.Pbgra32, null, buffer, stride);
        result.Freeze();
        return result;
    }

    /// <summary>
    /// ｷｬｯｼｭの重さ
    /// </summary>
    /// <param name="bitmap"></param>
    /// <returns></returns>
    public long Measure(BitmapSource bitmap) => (long)bitmap.PixelWidth * bitmap.PixelHeight * 4;
}
