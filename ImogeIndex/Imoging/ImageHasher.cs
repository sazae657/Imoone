using ImogeIndex.Zenerators;
using SkiaSharp;
using System.Numerics;

namespace ImogeIndex.Imoging;

/// <summary>
/// ﾊｯｼｭ計算
/// </summary>
public sealed class ImageHasher
{
    const int PHashZise = 64;
    const int PHashDctZise = 8;
    static readonly float[] PHashCosines = CreatePHashCosines();
    const int WHashZise = 32;
    const int WHashLowFrequencyZise = 8;

    readonly static SKSamplingOptions samplingOptions = new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear);

    /// <summary>
    /// おまとめﾊｯｼｭ計算
    /// </summary>
    /// <param name="tr"></param>
    public static void CalcHash(ImageTransaction tr)
    {
        tr.PHash = Perseptual(tr.Bitmap);
        tr.AHash = Average(tr.Bitmap);
        tr.DHash = Difference(tr.Bitmap);
        tr.WHash = Wavelet(tr.Bitmap);
    }

    /// <summary>
    /// ﾊﾐﾝｸﾞ距離の計算
    /// </summary>
    /// <param name="hash1"></param>
    /// <param name="hash2"></param>
    /// <returns></returns>
    public static int GetHammingDistance(ulong hash1, ulong hash2) {
        ulong xor = hash1 ^ hash2;
        return BitOperations.PopCount(xor);
    }

    /// <summary>
    /// Perseptual Hash計算
    /// </summary>
    /// <param name="betmap"></param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    public static ulong Perseptual(SKBitmap betmap)
    {
        // PHashZise x PHashZiseにﾘｻｲｽﾞ
        using var resized = betmap.Resize(new SKImageInfo(PHashZise, PHashZise), samplingOptions);
        if (resized == null) {
            throw new InvalidOperationException(Properties.Resources.IM_E_ResizeFailed);
        }

        Span<float> pixels = stackalloc float[PHashZise * PHashZise];
        var colorPixels = resized.Pixels;

        for (var i = 0; i < pixels.Length; i++) {
            var color = colorPixels[i];
            // 輝度近似
            pixels[i] = ((77 * color.Red) + (150 * color.Green) + (29 * color.Blue)) >> 8;
        }

        // 2次元DCTを縦横に分離
        Span<float> horizontal = stackalloc float[PHashDctZise * PHashZise];
        for (var u = 0; u < PHashDctZise; u++) {
            var cosineOffset = u * PHashZise;
            var horizontalOffset = u * PHashZise;
            for (var y = 0; y < PHashZise; y++) {
                var sum = 0f;
                var pixelOffset = y * PHashZise;
                for (var x = 0; x < PHashZise; x++) {
                    sum += pixels[pixelOffset + x] * PHashCosines[cosineOffset + x];
                }
                horizontal[horizontalOffset + y] = sum;
            }
        }

        Span<float> dct = stackalloc float[PHashDctZise * PHashDctZise];
        const float inverseSqrt2 = 0.7071067811865475f;
        for (var u = 0; u < PHashDctZise; u++) {
            for (var v = 0; v < PHashDctZise; v++) {
                var sum = 0f;
                var cosineOffset = v * PHashZise;
                for (var y = 0; y < PHashZise; y++) {
                    sum += horizontal[(u * PHashZise) + y] * PHashCosines[cosineOffset + y];
                }
                var scale = (u == 0 ? inverseSqrt2 : 1f) * (v == 0 ? inverseSqrt2 : 1f);
                dct[(u * PHashDctZise) + v] = sum * scale;
            }
        }

        // DCT(直流成分 dct[0,0]を除く)平均値計算
        var total = 0f;
        for (var i = 1; i < dct.Length; i++) {
            total += dct[i];
        }
        var average = total / 63f;

        // ﾊｯｼｭ作り
        ulong hash = 0;
        for (var i = 0; i < dct.Length; i++) {
            if (dct[i] > average) {
                hash |= 1UL << (63 - i);
            }
        }

        return hash;
    }

    /// <summary>
    /// これｸｿ重いので余弦係数の事前計算
    /// </summary>
    /// <returns></returns>
    private static float[] CreatePHashCosines()
    {
        var cosines = new float[PHashDctZise * PHashZise];
        for (var freq = 0; freq < PHashDctZise; freq++) {
            for (var pos = 0; pos < PHashZise; pos++) {
                cosines[(freq * PHashZise) + pos] =
                    (float)Math.Cos(((2 * pos + 1) * freq * Math.PI) / (2 * PHashZise));
            }
        }
        return cosines;
    }

    /// <summary>
    /// Wavelet Hash計算
    /// </summary>
    /// <param name="betmap"></param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    public static ulong Wavelet(SKBitmap betmap)
    {
        // WHashSize x WHashSizeにﾘｻｲｽﾞしてｸﾞﾚーｽｹーﾙ化
        using var resized = betmap.Resize(new SKImageInfo(WHashZise, WHashZise), samplingOptions);
        if (resized == null) {
            throw new InvalidOperationException(Properties.Resources.IM_E_ResizeFailed);
        }

        Span<float> coff = stackalloc float[WHashZise * WHashZise];
        var colorPixels = resized.Pixels;
        for (var i = 0; i < coff.Length; i++) {
            var color = colorPixels[i];
            coff[i] = ((77 * color.Red) + (150 * color.Green) + (29 * color.Blue)) >> 8;
        }

        // 2次元Haar変換を2段行して左上8x8に低周波成分を集める
        Span<float> work = stackalloc float[WHashZise];
        for (var size = WHashZise; size > WHashLowFrequencyZise; size /= 2) {
            HaarTransformRows(coff, work, size);
            HaarTransformColumns(coff, work, size);
        }

        Span<float> lowFreq = stackalloc float[WHashLowFrequencyZise * WHashLowFrequencyZise];
        for (var y = 0; y < WHashLowFrequencyZise; y++) {
            coff.Slice(y * WHashZise, WHashLowFrequencyZise)
                .CopyTo(lowFreq.Slice(y * WHashLowFrequencyZise, WHashLowFrequencyZise));
        }

        // 直流成分を除いた中央値を閾値にする
        Span<float> sorted = stackalloc float[lowFreq.Length - 1];
        lowFreq.Slice(1).CopyTo(sorted);
        sorted.Sort();
        var median = sorted[sorted.Length / 2];

        ulong hash = 0;
        for (var i = 0; i < lowFreq.Length; i++) {
            if (lowFreq[i] > median) {
                hash |= 1UL << (63 - i);
            }
        }

        return hash;
    }


    /// <summary>
    /// 行方向のHaar変換
    /// </summary>
    /// <param name="coef"></param>
    /// <param name="work"></param>
    /// <param name="size"></param>
    private static void HaarTransformRows(Span<float> coef, Span<float> work, int size)
    {
        var half = size / 2;
        for (var y = 0; y < size; y++) {
            var offset = y * WHashZise;
            for (var x = 0; x < half; x++) {
                var left = coef[offset + (x * 2)];
                var right = coef[offset + (x * 2) + 1];
                work[x] = (left + right) * 0.5f;
                work[half + x] = (left - right) * 0.5f;
            }
            work.Slice(0, size).CopyTo(coef.Slice(offset, size));
        }
    }


    /// <summary>
    /// 列方向のHaar変換
    /// </summary>
    /// <param name="coef"></param>
    /// <param name="work"></param>
    /// <param name="size"></param>
    private static void HaarTransformColumns(Span<float> coef, Span<float> work, int size)
    {
        var half = size / 2;
        for (var x = 0; x < size; x++) {
            for (var y = 0; y < half; y++) {
                var top = coef[(y * 2 * WHashZise) + x];
                var bottom = coef[(((y * 2) + 1) * WHashZise) + x];
                work[y] = (top + bottom) * 0.5f;
                work[half + y] = (top - bottom) * 0.5f;
            }
            for (var y = 0; y < size; y++) {
                coef[(y * WHashZise) + x] = work[y];
            }
        }
    }

    /// <summary>
    /// Average Hash計算
    /// </summary>
    /// <param name="betmap"></param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    public static ulong Average(SKBitmap betmap)
    {
        const int zise = 8;

        // 8x8 にﾘｻｲｽﾞ
        using var resized = betmap.Resize(new SKImageInfo(zise, zise), samplingOptions);
        if (resized == null)
        {
            throw new InvalidOperationException(Properties.Resources.IM_E_ResizeFailed);
        }

        var pixels = new float[64];
        var colorPixels = resized.Pixels;
        var sum = 0f;

        // ｸﾞﾚーｽｹーﾙにして輝度の合計を出す
        for (int i = 0; i < 64; i++)
        {
            SKColor color = colorPixels[i];
            var luminance = (color.Red * 0.299f) + (color.Green * 0.587f) + (color.Blue * 0.114f);
            pixels[i] = luminance;
            sum += luminance;
        }

        float average = sum / 64f;
        ulong hash = 0;

        // ﾊｯｼｭ値作る
        for (int i = 0; i < 64; i++) {
            if (pixels[i] >= average) {
                hash |= (1UL << (63 - i));
            }
        }

        return hash;
    }

    /// <summary>
    /// Difference Hash計算
    /// </summary>
    /// <param name="betmap"></param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    public static ulong Difference(SKBitmap betmap)
    {
        const int width = 9;
        const int height = 8;

        // 9x8 にﾘｻｲｽﾞ
        using var resized = betmap.Resize(new SKImageInfo(width, height), samplingOptions);
        if (resized == null)
        {
            throw new InvalidOperationException(Properties.Resources.IM_E_ResizeFailed);
        }

        var colorPixels = resized.Pixels;
        var pixels = new float[width, height];

        // ｸﾞﾚーｽｹーﾙ化
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                SKColor color = colorPixels[y * width + x];
                pixels[x, y] = (color.Red * 0.299f) + (color.Green * 0.587f) + (color.Blue * 0.114f);
            }
        }

        ulong hash = 0;
        int bitIndex = 0;

        //  隣接ﾋﾟｸｾﾙの比較とﾊｯｼｭ作り
        for (var y = 0; y < height; y++) {
            for (var x = 0; x < width - 1; x++) {
                // 左が右より明るい(または等しい)場合は1
                if (pixels[x, y] >= pixels[x + 1, y]) {
                    hash |= (1UL << (63 - bitIndex));
                }
                bitIndex++;
            }
        }

        return hash;
    }
}

