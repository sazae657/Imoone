using ImageMagick;
using ImogeIndex.Common;
using ImogeIndex.Zenerators;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using SkiaSharp;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ImogeIndex.Taggers;

/// <summary>
/// Camie Taggerを使う実装
/// </summary>
internal class CamieTagger : TaggerBase {
    /// <summary>
    /// Lanczosに渡す画像の長辺を出力ｻｲｽﾞの何倍までに抑えるか
    /// </summary>
    internal const int PrescaleFactor = 2;

    /// <summary>
    /// ImageNet正規化の平均(RGB)
    /// </summary>
    static readonly float[] Mean = [0.485f, 0.456f, 0.406f];

    /// <summary>
    /// ImageNet正規化の標準偏差(RGB)
    /// </summary>
    static readonly float[] Std = [0.229f, 0.224f, 0.225f];

    /// <summary>
    /// ﾊﾟﾃﾞｨﾝｸﾞ色
    /// </summary>
    static readonly byte[] PadColor = [124, 116, 104];

    static CamieTagger() {
    }

    public CamieTagger(PreparedSettings setty) : base(setty) {
    }

    public override Task Sanitize(DirectoryInfo cacheDir, CancellationToken token) {
        return Task.Run(async() => {
            DevicePolicy = setting.DevicePolicy;
            Threshold = setting.Threshold;
            CharacterThreshold = setting.CharacterThreshold;
            Threads = setting.Threads.Positive();
            CacheDir = cacheDir;
            var tgm = ExpandingClass(setting.Model);
            if (!File.Exists(tgm)) {
                throw new DirectoryNotFoundException(Properties.Resources.TW_E_ModelNotExists.Morfat(tgm));
            }
            ModelPath = tgm.AsFileInfo();
            await LoadMetadatum(token);
            // ﾓﾃﾞﾙﾛーﾄﾞは途中で止められないので始める前に確認する
            token.ThrowIfCancellationRequested();
            LoadModel();
        }, token);
    }

    class TagMapping {
        [JsonPropertyName("tag_to_idx")] public Dictionary<string, int> TagToIdx { get; set; } = new();
        [JsonPropertyName("idx_to_tag")] public Dictionary<int, string> IdxToTag { get; set; } = new();
        [JsonPropertyName("tag_to_category")] public Dictionary<string, string> TagToCategory { get; set; } = new();
    }

    class DatasetInfo {
        [JsonPropertyName("total_tags")] public int TotalTags { get; set; }
        [JsonPropertyName("categories")] public string[] Categories { get; set; } = Array.Empty<string>();
        [JsonPropertyName("tag_mapping")] public TagMapping? TagMapping { get; set; }            
    }
    class ModelInfo {
        [JsonPropertyName("img_size")] public int ImgSize { get; set; }
    }

    class JaxonRoot {
        [JsonPropertyName("model_info")] public ModelInfo ModelInfo { get; set; } = new();
        [JsonPropertyName("dataset_info")] public DatasetInfo? DatasetInfo { get; set; }
    }

    ModelInfo modelInfo = new();
    TagMapping tagMapping = new();

    async Task LoadMetadatum(CancellationToken token) {
        if (ModelPath == null) {
            throw new ArgumentNullException(nameof(ModelPath));
        }
        FileInfo csv;
        if (string.IsNullOrEmpty(setting.Tags)) {
            var ssv = $"{ModelPath.GetNameWithoutExtension()}-metadata.json";
            csv = ModelPath.Directory!.CombineFile(ssv);
        }
        else {
            var tgv = ExpandingClass(setting.Tags);
            if (File.Exists(tgv)) {
                csv = tgv.AsFileInfo();
            }
            else {
                csv = ModelPath.Directory!.CombineFile(tgv);
            }
        }

        if (!csv.Exists) {
            var nssv = $"{ModelPath.GetNameWithoutExtension()}-metadata.json";
            csv = ModelPath.Directory!.CombineFile(nssv);
            if (!csv.Exists) {
                throw new FileNotFoundException(Properties.Resources.TW_E_MetadataNotExists.Morfat(csv), csv.FullName);
            }
        }
        OnMessage?.Invoke(Properties.Resources.TW_LoadingTag.Morfat(csv.FullName));
        using var fs = csv.OpenRead();
        var jaxon = await JsonSerializer.DeserializeAsync<JaxonRoot>(fs, JsonSerializerOptions.Default, token);
        if (jaxon == null) {
            throw new Exception(Properties.Resources.TW_E_MetadataLoadFailed.Morfat(csv));
        }

        modelInfo = jaxon.ModelInfo;
        if (jaxon.DatasetInfo?.TagMapping != null) {
            tagMapping = jaxon.DatasetInfo.TagMapping;
        }
        OnMessage?.Invoke(Properties.Resources.TW_LoadingTag.Morfat(csv.FullName));
    }

    void LoadModel() {
        if (ModelPath == null) {
            throw new ArgumentNullException(nameof(ModelPath));
        }
        var mudel = ModelPath;
        if (!mudel.Exists) {
            throw new FileNotFoundException(Properties.Resources.TW_E_ModelNotExists.Morfat(mudel), mudel.FullName);
        }

        try {
            LoadModelDefault();
            if (model == null) {
                throw new InvalidOperationException();
            }
            var meta = model.InputMetadata.First();
            var ota = model.OutputMetadata.First();
            shape = new Shape
            {
                InputName = meta.Key,
                OutputName = ota.Key,
                Height = meta.Value.Dimensions[2],
                Width = meta.Value.Dimensions[3]
            };
        }
        catch (Exception) {
            model?.Dispose();
            model = null;
            throw;
        }
    }

    class Cbt {
        public required string Category { get; set; }
        public required string Tag { get; set; }
        public float Score { get; set; } = 0f;
        public int Index { get; set; } = 0;
    }


    public override TaggingResult Predict(ImageTransaction img) {
        var source = img.Bitmap;
        if (source == null) {
            throw new InvalidOperationException(Properties.Resources.TW_E_ImageIsNull);
        }
        if (model == null) {
            throw new InvalidOperationException(Properties.Resources.TW_E_ModelNotLoaded);
        }

        var input = ToTensor(source, shape.Width);
        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor(shape.InputName, input)
        };
        try {
            using IDisposableReadOnlyCollection<DisposableNamedOnnxValue> results = model.Run(inputs);
            var logits =
                (results.Count >= 2 ? results[1] : results[0]).AsEnumerable<float>().ToArray();
            // しぐもる
            var probs = logits.Select(x => 1.0f / (1.0f + MathF.Exp(-x))).ToArray();
            var threshold = Math.Min(Threshold, CharacterThreshold);
            int[] indices = probs
                .Select((prob, index) => (prob, index))
                .Where(x => x.prob >= threshold)
                .Select(x => x.index)
                .ToArray();

            var cbts = new List<Cbt>();
            foreach (var i in indices) {
                if (!tagMapping.IdxToTag.TryGetValue(i, out var tagName)) {
                    continue;
                }
                if (!tagMapping.TagToCategory.TryGetValue(tagName, out var category)) {
                    continue;
                }
                if (category == "rating") {
                    tagName = tagName.Replace("rating_", "");
                }
                if (category == "year") {
                    tagName = tagName.Replace("year_", "");
                }
                var prob = probs[i];
                cbts.Add(new Cbt{ Category = category, Tag = tagName, Score = prob, Index = i });
            }

            var tr = new TaggingResult();
            foreach (var cc in cbts.Where(w => w.Category == "character")) {
                if (cc.Score > CharacterThreshold) {
                    tr.Character.Add(new Tag { Category = 0, Index = cc.Index, Name = cc.Tag, Score = cc.Score });
                }
            }

            foreach (var cc in cbts.Where(w => w.Category == "rating")) {
                if (cc.Score > Threshold) {
                    tr.Rating.Add(new Tag { Category = 0, Index = cc.Index, Name = cc.Tag, Score = cc.Score });
                }
            }

            foreach (var cc in cbts.Where(w => w.Category == "general" ||
                w.Category == "copyright" || w.Category == "artist" || w.Category == "meta")) {
                if (cc.Score > Threshold) {
                    tr.Tags.Add(new Tag { Category = 0, Index = cc.Index, Name = cc.Tag, Score = cc.Score });
                }
            }

            return tr;
        }
        catch {
            throw;
        }
    }


    /// <summary>
    /// SKBitpamをTensorに
    /// </summary>
    /// <param name="betmap"></param>
    /// <param name="zise"></param>
    /// <returns></returns>
    internal static DenseTensor<float> ToTensor(SKBitmap betmap, int zise = 512) {
        // IndexProzessorで前処理済みならﾘｻｲｽﾞを省略
        if (betmap.Width == zise && betmap.Height == zise
            && betmap.ColorType == SKColorType.Rgba8888) {
            return ToTensorYJ(betmap, zise);
        }
        using var prepared = PrepareImage(betmap, zise);
        return ToTensorYJ(prepared, zise);
    }

    /// <summary>
    /// 前処理済みの画像をNCHWに
    /// </summary>
    /// <param name="prepared"></param>
    /// <param name="zise"></param>
    /// <returns></returns>
    static DenseTensor<float> ToTensorYJ(SKBitmap prepared, int zise) {
        var tensor = new DenseTensor<float>([1, 3, zise, zise]);
        int plane = zise * zise;
        Span<float> dst = tensor.Buffer.Span;
        Span<float> r = dst.Slice(0, plane);
        Span<float> g = dst.Slice(plane, plane);
        Span<float> b = dst.Slice(plane * 2, plane);
        ReadOnlySpan<byte> pixels = prepared.GetPixelSpan();
        int rowBytes = prepared.RowBytes;
        // ImageNet正規化: (v/255 - mean) / std = v * scale + bias
        float rs = 1f / (255f * Std[0]), rb = -Mean[0] / Std[0];
        float gs = 1f / (255f * Std[1]), gb = -Mean[1] / Std[1];
        float bs = 1f / (255f * Std[2]), bb = -Mean[2] / Std[2];
        for (int y = 0; y < zise; y++) {
            var row = pixels.Slice(y * rowBytes, zise * 4);
            int o = y * zise;
            for (int x = 0, s = 0; x < zise; x++, s += 4) {
                r[o + x] = row[s] * rs + rb;
                g[o + x] = row[s + 1] * gs + gb;
                b[o + x] = row[s + 2] * bs + bb;
            }
        }
        return tensor;
    }

    /// <summary>
    /// 前処理
    /// </summary>
    /// <param name="betmap"></param>
    /// <param name="imageSize"></param>
    /// <returns></returns>
    internal static SKBitmap PrepareImage(SKBitmap betmap, int imageSize) {
        ArgumentNullException.ThrowIfNull(betmap);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(imageSize);

        double aspect = (double)betmap.Width / betmap.Height;
        int newWidth = aspect > 1 ? imageSize : (int)(imageSize * aspect);
        int newHeight = aspect > 1 ? (int)(imageSize / aspect) : imageSize;
        if (newWidth == 0 || newHeight == 0) {
            throw new ArgumentException(Properties.Resources.TW_E_ImageTooSmall, nameof(betmap));
        }

        using var normalized = Normalize(betmap);
        var source = normalized ?? betmap;
        using var reduced = Prescale(source, imageSize * PrescaleFactor);
        source = reduced ?? source;

        using var resized = new MagickImage();
        // 4ﾊﾞｲﾄ画素のまま直接渡し
        // αはﾘｻｲｽﾞ前に無効化する(PILのconvert('RGB')とだいたい同じになる…はず)
        resized.ReadPixels(source.GetPixelSpan(), new PixelReadSettings(
            (uint)source.Width, (uint)source.Height, StorageType.Char,
            source.ColorType == SKColorType.Bgra8888 ? PixelMapping.BGRA : PixelMapping.RGBA));
        resized.Alpha(AlphaAction.Off);
        resized.FilterType = FilterType.Lanczos;
        resized.Resize(new MagickGeometry((uint)newWidth, (uint)newHeight) { IgnoreAspectRatio = true });

        using var scaledPixels = resized.GetPixelsUnsafe();
        var scaled = scaledPixels.ToByteArray(PixelMapping.RGB)
            ?? throw new InvalidOperationException(Properties.Resources.TW_E_GetPixelsFailed);

        var prepared = new SKBitmap(new SKImageInfo(imageSize, imageSize,
            SKColorType.Rgba8888, SKAlphaType.Opaque));
        try {
            // SKBitmapへ(余白はPadColorで埋める)
            Span<byte> data = GetWritablePixels(prepared);
            int rowBytes = prepared.RowBytes;
            for (int y = 0; y < imageSize; y++) {
                var row = data.Slice(y * rowBytes, imageSize * 4);
                for (int i = 0; i < row.Length; i += 4) {
                    row[i] = PadColor[0];
                    row[i + 1] = PadColor[1];
                    row[i + 2] = PadColor[2];
                    row[i + 3] = 255;
                }
            }
            int pasteX = (imageSize - newWidth) / 2;
            int pasteY = (imageSize - newHeight) / 2;
            for (int y = 0; y < newHeight; y++) {
                var src = scaled.AsSpan(y * newWidth * 3, newWidth * 3);
                var row = data.Slice((pasteY + y) * rowBytes + pasteX * 4, newWidth * 4);
                for (int x = 0, s = 0, d = 0; x < newWidth; x++, s += 3, d += 4) {
                    row[d] = src[s];
                    row[d + 1] = src[s + 1];
                    row[d + 2] = src[s + 2];
                }
            }
            return prepared;
        }
        catch {
            prepared.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Magick形式(8888、非乗算)にする
    /// </summary>
    /// <param name="betmap"></param>
    /// <returns>変換不要ならnull</returns>
    static SKBitmap? Normalize(SKBitmap betmap) {
        if ((betmap.ColorType == SKColorType.Bgra8888 || betmap.ColorType == SKColorType.Rgba8888)
            && betmap.AlphaType != SKAlphaType.Premul
            && betmap.RowBytes == betmap.Width * 4) {
            return null;
        }
        using var src = betmap.PeekPixels();
        var dst = new SKBitmap(new SKImageInfo(betmap.Width, betmap.Height,
            SKColorType.Rgba8888, SKAlphaType.Unpremul));
        if (!src.ReadPixels(dst.Info, dst.GetPixels(), dst.RowBytes, 0, 0)) {
            dst.Dispose();
            throw new InvalidOperationException(Properties.Resources.TW_E_ReadRgbFailed);
        }
        return dst;
    }

    /// <summary>
    /// 長辺がmaxEdgeを超えていたらmaxEdgeまで縮小する
    /// </summary>
    /// <param name="betmap"></param>
    /// <param name="maxEdge"></param>
    /// <returns></returns>
    static SKBitmap? Prescale(SKBitmap betmap, int maxEdge) {
        int longEdge = Math.Max(betmap.Width, betmap.Height);
        if (longEdge <= maxEdge) {
            return null;
        }
        double scale = (double)maxEdge / longEdge;
        int width = Math.Max(1, (int)Math.Round(betmap.Width * scale));
        int height = Math.Max(1, (int)Math.Round(betmap.Height * scale));
        using var src = new SKPixmap(betmap.Info.WithAlphaType(SKAlphaType.Opaque),
            betmap.GetPixels(), betmap.RowBytes);
        var dst = new SKBitmap(new SKImageInfo(width, height, betmap.ColorType, SKAlphaType.Opaque));
        using var dstPixels = dst.PeekPixels();
        if (!src.ScalePixels(dstPixels, new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear))) {
            dst.Dispose();
            throw new InvalidOperationException(Properties.Resources.TW_E_FailedToResize);
        }
        return dst;
    }

    /// <summary>
    /// 書き込みﾊﾞｯﾌｧー取得
    /// </summary>
    /// <param name="betmap"></param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    static unsafe Span<byte> GetWritablePixels(SKBitmap betmap) {
        var ptr = betmap.GetPixels();
        if (ptr == IntPtr.Zero) {
            throw new InvalidOperationException(Properties.Resources.TW_E_PixelBufferFailed);
        }
        return new Span<byte>((void*)ptr, checked(betmap.RowBytes * betmap.Height));
    }
}
