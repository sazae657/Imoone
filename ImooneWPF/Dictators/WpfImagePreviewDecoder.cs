using ImageMagick;
using ImogeIndex.Imoging;
using ImogeIndex.Web6.Dictators;
using ImogeIndex.Mur;
using SkiaSharp;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ImooneWPF.Dictators;

/// <summary>
/// 画像ﾌﾟﾚﾋﾞｭーのWPF用ﾃﾞｺーﾀﾞー
/// </summary>
internal sealed class WpfImagePreviewDecoder : IImagePreviewDecoder<BitmapSource>
{
    public static WpfImagePreviewDecoder Instance { get; } = new();

    /// <summary>
    /// 事前作成ｻﾑﾈ
    /// </summary>
    /// <param name="stream"></param>
    /// <returns></returns>
    public BitmapSource? DecodeThumbnail(Stream stream)
    {
        var betmap = new BitmapImage();
        betmap.BeginInit();
        betmap.StreamSource = stream;
        betmap.CacheOption = BitmapCacheOption.OnLoad;
        betmap.EndInit();
        betmap.Freeze();
        return betmap;
    }

    /// <summary>
    /// 画像を読み込む(WPFでﾀﾞﾒならMagickImage)
    /// </summary>
    /// <param name="fileItem"></param>
    /// <param name="maxSize">長辺の上限(これより大きい画像は縮めてﾃﾞｺーﾄﾞ、0なら原寸)</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public BitmapSource? DecodeImage(FileItem fileItem, int maxSize, CancellationToken cancellationToken)
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
}
