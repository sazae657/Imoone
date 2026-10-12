using ImageMagick;
using ImogeIndex.Common;
using ImogeIndex.Sources;
using SkiaSharp;

namespace ImogeIndex.Imoging;

public static class SKBitmapExtensions {
    /// <summary>
    /// SKBitmapをPNGにしてbyte[]で返す
    /// </summary>
    /// <param name="betmap"></param>
    /// <returns></returns>
    public static byte[] AsByteArray(this SKBitmap betmap)　{
        using (var stream = new MemoryStream())
        using (var data = betmap.Encode(SKEncodedImageFormat.Png, quality: 100)) {
            data.SaveTo(stream);
            return stream.ToArray();
        }
    }

    /// <summary>
    /// 長辺指定でﾘｻｲｽﾞ
    /// </summary>
    /// <param name="betmap"></param>
    /// <param name="maxEdgeLength"></param>
    /// <returns></returns>
    public static SKBitmap ResizeByMaxEdge(this SKBitmap betmap, int maxEdgeLength) {
        int sw = betmap.Width;
        int sh = betmap.Height;

        double ratio = (sw > sh)
            ? (double)maxEdgeLength / sw
            : (double)maxEdgeLength / sh;

        int dw = (int)Math.Round(sw * ratio);
        int dh = (int)Math.Round(sh * ratio);

        dw = dw.Positive();
        dh = dh.Positive();

        var info = new SKImageInfo(dw, dh, betmap.ColorType, betmap.AlphaType);
        var target = new SKBitmap(info);
        try {
            using var csv = new SKCanvas(target);
            using var paint = new SKPaint { IsAntialias = true };
            csv.Clear(SKColors.Transparent);
            csv.DrawBitmap(betmap,
                new SKRect(0, 0, sw, sh),
                new SKRect(0, 0, dw, dh),
                new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear),
                paint);
        }
        catch {
            target.Dispose();
            throw;
        }

        return target;
    }
}

/// <summary>
/// 画像読み込みﾍﾙﾊﾟー
/// </summary>
public class ImageLoader
{
    /// <summary>
    /// 透明画素のRGBが失われないようにﾃﾞｺーﾄﾞ
    /// </summary>
    /// <param name="codec"></param>
    /// <param name="minLongEdge"></param>
    /// <param name="origin"></param>
    internal static SKBitmap? Decode(SKCodec codec, int minLongEdge = 0,
        SKEncodedOrigin origin = SKEncodedOrigin.TopLeft)
    {
        var info = codec.Info;
        if (info.AlphaType != SKAlphaType.Opaque) {
            info.AlphaType = SKAlphaType.Unpremul;
        }
        if (minLongEdge > 0) {
            var size = CalcScaledSize(codec, minLongEdge);
            info.Width = size.Width;
            info.Height = size.Height;
        }
        var bitmap = SKBitmap.Decode(codec, info);
        return bitmap == null ? null : Orient(bitmap, ResolveOrigin(codec, origin));
    }

    /// <summary>
    /// 回転方向を解決
    /// </summary>
    /// <param name="codec"></param>
    /// <param name="containerOrigin"></param>
    /// <returns></returns>
    static SKEncodedOrigin ResolveOrigin(SKCodec codec, SKEncodedOrigin containerOrigin)
        => codec.EncodedOrigin != SKEncodedOrigin.TopLeft ? codec.EncodedOrigin : containerOrigin;

    /// <summary>
    /// TIFFｺﾝﾃﾅ(cr2)のIFD0の回転情報を読む
    /// SkiaはRAWの埋め込みﾌﾟﾚﾋﾞｭーJPEGをﾃﾞｺーﾄﾞするが、回転情報はｺﾝﾃﾅ側にしか無いのでそのままだと縦画像が寝る
    /// ｷﾔﾉﾝしか持ってないので他の機種は知らん
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    internal static SKEncodedOrigin ReadContainerOrigin(Stream input) {
        if (!input.CanSeek) {
            return SKEncodedOrigin.TopLeft;
        }
        var position = input.Position;
        try {
            Span<byte> magic = stackalloc byte[4];
            if (input.ReadAtLeast(magic, magic.Length, throwOnEndOfStream: false) != magic.Length) {
                return SKEncodedOrigin.TopLeft;
            }
            // II*\0 または MM\0*
            bool tiff = (magic[0] == 'I' && magic[1] == 'I' && magic[2] == 0x2A && magic[3] == 0) ||
                (magic[0] == 'M' && magic[1] == 'M' && magic[2] == 0 && magic[3] == 0x2A);
            if (!tiff) {
                return SKEncodedOrigin.TopLeft;
            }
            input.Position = position;
            var ifd0 = MetadataExtractor.Formats.Tiff.TiffMetadataReader.ReadMetadata(input)
                .OfType<MetadataExtractor.Formats.Exif.ExifIfd0Directory>()
                .FirstOrDefault();
            if (ifd0 != null &&
                MetadataExtractor.DirectoryExtensions.TryGetInt32(ifd0,
                    MetadataExtractor.Formats.Exif.ExifDirectoryBase.TagOrientation, out var orientation) &&
                orientation >= (int)SKEncodedOrigin.TopLeft && orientation <= (int)SKEncodedOrigin.LeftBottom) {
                return (SKEncodedOrigin)orientation;
            }
            return SKEncodedOrigin.TopLeft;
        }
        catch {
            return SKEncodedOrigin.TopLeft;
        }
        finally {
            input.Position = position;
        }
    }

    /// <summary>
    /// EXIFの回転情報を読む
    /// </summary>
    /// <param name="path"></param>
    /// <returns></returns>
    public static SKEncodedOrigin ReadOrigin(string path) {
        try {
            using var input = File.OpenRead(path);
            var containerOrigin = ReadContainerOrigin(input);
            using var codec = SKCodec.Create(input);
            return codec == null ? containerOrigin : ResolveOrigin(codec, containerOrigin);
        }
        catch {
            return SKEncodedOrigin.TopLeft;
        }
    }

    /// <summary>
    /// 縦横が入れ替わる回転？
    /// </summary>
    /// <param name="origin"></param>
    /// <returns></returns>
    public static bool SwapsAxes(SKEncodedOrigin origin) => origin is
        SKEncodedOrigin.LeftTop or SKEncodedOrigin.RightTop or
        SKEncodedOrigin.RightBottom or SKEncodedOrigin.LeftBottom;

    /// <summary>
    /// 回転情報に従って正立させる
    /// </summary>
    /// <param name="betmap"></param>
    /// <param name="origin"></param>
    /// <returns></returns>
    internal static unsafe SKBitmap Orient(SKBitmap betmap, SKEncodedOrigin origin) {
        if (origin == SKEncodedOrigin.TopLeft || origin < SKEncodedOrigin.TopLeft || origin > SKEncodedOrigin.LeftBottom) {
            return betmap;
        }
        int sw = betmap.Width;
        int sh = betmap.Height;
        bool swap = SwapsAxes(origin);
        int dw = swap ? sh : sw;
        int dh = swap ? sw : sh;
        int bpp = betmap.BytesPerPixel;
        var info = new SKImageInfo(dw, dh, betmap.ColorType, betmap.AlphaType, betmap.ColorSpace);
        var result = new SKBitmap(info);
        try {
            var sp = (byte*)betmap.GetPixels();
            var dp = (byte*)result.GetPixels();
            if (sp == null || dp == null || bpp <= 0) {
                throw new InvalidOperationException(Properties.Resources.IM_E_GetPixelsFailed);
            }
            long srb = betmap.RowBytes;
            long drb = result.RowBytes;

            // 出力画素(dx, dy)に対応する入力画素(sx, sy)
            (long sx, long sy) Map(long dx, long dy) => origin switch {
                SKEncodedOrigin.TopRight => (sw - 1 - dx, dy),
                SKEncodedOrigin.BottomRight => (sw - 1 - dx, sh - 1 - dy),
                SKEncodedOrigin.BottomLeft => (dx, sh - 1 - dy),
                SKEncodedOrigin.LeftTop => (dy, dx),
                SKEncodedOrigin.RightTop => (dy, sh - 1 - dx),
                SKEncodedOrigin.RightBottom => (sw - 1 - dy, sh - 1 - dx),
                SKEncodedOrigin.LeftBottom => (sw - 1 - dy, dx),
                _ => (dx, dy),
            };

            for (int dy = 0; dy < dh; dy++) {
                var (bx, by) = Map(0, dy);
                var (nx, ny) = Map(1, dy);
                long step = (ny - by) * srb + (nx - bx) * bpp;
                byte* s = sp + by * srb + bx * bpp;
                byte* d = dp + dy * drb;
                switch (bpp) {
                    case 4:
                        for (int dx = 0; dx < dw; dx++, s += step, d += 4) {
                            *(uint*)d = *(uint*)s;
                        }
                        break;
                    case 1:
                        for (int dx = 0; dx < dw; dx++, s += step, d++) {
                            *d = *s;
                        }
                        break;
                    default:
                        for (int dx = 0; dx < dw; dx++, s += step, d += bpp) {
                            Buffer.MemoryCopy(s, d, bpp, bpp);
                        }
                        break;
                }
            }
            result.NotifyPixelsChanged();
        }
        catch {
            result.Dispose();
            throw;
        }
        betmap.Dispose();
        return result;
    }


    /// <summary>
    /// 長辺がminLongEdge以上になる最小のﾃﾞｺーﾄﾞｻｲｽﾞ計算(縮小ﾃﾞｺーﾄﾞ非対応のﾔﾂは元のｻｲｽﾞ)
    /// </summary>
    /// <param name="codec"></param>
    /// <param name="minLongEdge"></param>
    /// <returns></returns>
    static SKSizeI CalcScaledSize(SKCodec codec, int minLongEdge) {
        // JPEGのDCTｽｹーﾘﾝｸﾞは1/8まで
        foreach (var scale in new[] { 0.125f, 0.25f, 0.5f }) {
            var size = codec.GetScaledDimensions(scale);
            if (Math.Max(size.Width, size.Height) >= minLongEdge) {
                return size;
            }
        }
        return codec.Info.Size;
    }

    /// <summary>
    /// MagickImageで読み込み
    /// </summary>
    /// <param name="image"></param>
    /// <returns></returns>
    internal static unsafe SKBitmap FromMagick(MagickImage image) {
        // 回転情報を適用して正立させる
        image.AutoOrient();
        // CMYKやLinearRGB等はそのまま取り出すと色が壊れるのでsRGBへ変換する
        if (image.ColorSpace != ColorSpace.sRGB) {
            image.ColorSpace = ColorSpace.sRGB;
        }
        int width = checked((int)image.Width);
        int height = checked((int)image.Height);
        var alphaType = image.HasAlpha ? SKAlphaType.Unpremul : SKAlphaType.Opaque;
        var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, alphaType));
        try {
            var ptr = bitmap.GetPixels();
            if (ptr == IntPtr.Zero) {
                throw new InvalidOperationException(Properties.Resources.IM_E_GetPixelsFailed);
            }
            using var pixels = image.GetPixelsUnsafe();
            var rgba = pixels.ToByteArray(PixelMapping.RGBA)
                ?? throw new InvalidOperationException(Properties.Resources.IM_E_GetPixelFailed);
            int skb = width * 4;
            int dkb = bitmap.RowBytes;
            var dst = new Span<byte>((void*)ptr, checked(dkb * height));
            if (skb == dkb) {
                rgba.AsSpan(0, skb * height).CopyTo(dst);
            }
            else {
                for (int y = 0; y < height; y++) {
                    rgba.AsSpan(y * skb, skb).CopyTo(dst.Slice(y * dkb));
                }
            }
            bitmap.NotifyPixelsChanged();
            return bitmap;
        }
        catch {
            bitmap.Dispose();
            throw;
        }
    }

    /// <summary>
    /// ﾒﾓﾘ上の画像を読み込む
    /// </summary>
    /// <param name="bytes"></param>
    /// <param name="minLongEdge">0より大きければ、長辺がこの値を下回らない範囲で縮小ﾃﾞｺーﾄﾞする</param>
    /// <returns></returns>
    public static SKBitmap Load(byte[] bytes, int minLongEdge = 0)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        using (var input = new MemoryStream(bytes, writable: false)) {
            var containerOrigin = ReadContainerOrigin(input);
            using var codec = SKCodec.Create(input);
            var skb = codec == null ? null : Decode(codec, minLongEdge, containerOrigin);
            if (skb != null) {
                return skb;
            }
        }

        // ImageMagickにﾌｫーﾙﾊﾞｯｸ
        using var image = new MagickImage(bytes);
        return FromMagick(image);
    }

    /// <summary>
    /// ｽﾄﾘーﾑから読み込む
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    public static SKBitmap Load(Stream input)
    {
        ArgumentNullException.ThrowIfNull(input);
        using var buffer = new MemoryStream();
        input.CopyTo(buffer);
        return Load(buffer.ToArray());
    }

    /// <summary>
    /// 読み込む
    /// </summary>
    /// <param name="path"></param>
    /// <returns></returns>
    public static SKBitmap? Load(string path)
    {
        return Load(FileSourceRegistry.Instance.ReadAllBytes(path));
    }

    /// <summary>
    /// 読み込んで長辺指定でﾘｻｲｽﾞ
    /// </summary>
    /// <param name="path"></param>
    /// <param name="maxEdgeLength"></param>
    /// <returns></returns>
    public static SKBitmap? LoadAndResize(string path, int maxEdgeLength)
    {
        using var bmp = Load(path);
        if (bmp == null) {
            return null;
        }
        return bmp.ResizeByMaxEdge(maxEdgeLength);
    }
}
