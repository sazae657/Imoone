using ImogeIndex.Imoging;
using ImogeIndex.Web6.Dictators;

namespace ImogeIndex.Web6.Imaging;

/// <summary>
/// 一覧ｻﾑﾈのSkia版ﾃﾞｺーﾀﾞー
/// </summary>
public sealed class SkiaTileBitmapDecoder : ITileBitmapDecoder<BgraBitmap>
{
    public static SkiaTileBitmapDecoder Instance { get; } = new();

    /// <summary>
    /// BLOBから画像を作る
    /// </summary>
    /// <param name="bytes"></param>
    /// <param name="size">長辺の上限</param>
    /// <param name="portrait">縦長？(Skiaは長辺で縮めるので使わない)</param>
    /// <returns></returns>
    public BgraBitmap? Decode(byte[] bytes, int size, bool portrait)
    {
        // 縮小ﾃﾞｺーﾄﾞしてから長辺を上限内に収める
        using var bitmap = ImageLoader.Load(bytes, size);
        return BgraBitmap.From(bitmap, size);
    }

    /// <summary>
    /// ｷｬｯｼｭの重さ
    /// </summary>
    /// <param name="bitmap"></param>
    /// <returns></returns>
    public long Measure(BgraBitmap bitmap) => bitmap.Bytes;
}
