using ImageMagick;
using ImogeIndex.Sources;
using SkiaSharp;
using System.Security.Cryptography;

namespace ImogeIndex.Imoging;

/// <summary>
/// 画像読み込み結果
/// </summary>
public enum ImageReadStatus {
    Success,
    Unavailable,
    Corrupt,
    Unknown
}

/// <summary>
/// 画像読み込み結果ｵﾌﾞﾚｺーﾄﾞ
/// </summary>
/// <param name="Status"></param>
/// <param name="Bitmap"></param>
/// <param name="ContentHash"></param>
/// <param name="Exception"></param>
public sealed record ImageReadResult(ImageReadStatus Status, SKBitmap? Bitmap = null,
    string? ContentHash = null, Exception? Exception = null) {

    /// <summary>
    /// ExceptionからImageReadStatusに
    /// </summary>
    /// <param name="exception"></param>
    /// <returns></returns>
    public static ImageReadStatus Court(Exception exception) => exception switch {
        IOException or UnauthorizedAccessException => ImageReadStatus.Unavailable,
        MagickCorruptImageErrorException => ImageReadStatus.Corrupt,
        _ => ImageReadStatus.Unknown,
    };

    /// <summary>
    /// 画像読み込み
    /// </summary>
    /// <param name="path"></param>
    /// <param name="token"></param>
    /// <param name="minLongEdge"></param>
    /// <returns></returns>
    public static ImageReadResult Read(string path, CancellationToken token, int minLongEdge = 0) =>
        Read(FileSourceRegistry.Instance, path, token, minLongEdge);

    /// <summary>
    /// 供給元から画像読み込み
    /// </summary>
    /// <param name="sources"></param>
    /// <param name="location"></param>
    /// <param name="token"></param>
    /// <param name="minLongEdge"></param>
    /// <returns></returns>
    public static ImageReadResult Read(FileSourceRegistry sources, string location, CancellationToken token, int minLongEdge = 0) {
        byte[] bytes;
        try {
            token.ThrowIfCancellationRequested();
            bytes = sources.ReadAllBytes(location);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) {
            return new(Court(ex), Exception: ex);
        }
        return Read(bytes, token, minLongEdge);
    }

    /// <summary>
    /// 読み込み済みの画像をﾃﾞｺーﾄﾞ
    /// </summary>
    /// <param name="bytes"></param>
    /// <param name="token"></param>
    /// <param name="minLongEdge"></param>
    /// <returns></returns>
    public static ImageReadResult Read(byte[] bytes, CancellationToken token, int minLongEdge = 0) {
        string? hash = null;
        try {
            token.ThrowIfCancellationRequested();
            hash = Convert.ToHexString(SHA512.HashData(bytes)).ToLowerInvariant();
            return new(ImageReadStatus.Success, ImageLoader.Load(bytes, minLongEdge), hash);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) {
            return new(Court(ex), ContentHash: hash, Exception: ex);
        }
    }
}
