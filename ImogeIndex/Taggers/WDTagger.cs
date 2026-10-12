using ImogeIndex.Common;
using ImogeIndex.Zenerators;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using SkiaSharp;

namespace ImogeIndex.Taggers;


/// <summary>
/// WaifuDiffusion Taggerを使う実装
/// </summary>
internal class WDTagger : TaggerBase {
    List<Tag> tar = new();

    List<int> ratingIndexes = new();
    List<int> generalIndexes = new();
    List<int> characterIndexes = new();

    public WDTagger(PreparedSettings setty) : base(setty)
    {
    }

    public override Task Sanitize(DirectoryInfo cacheDir, CancellationToken token) {
        return Task.Run(() => {
            DevicePolicy = setting.DevicePolicy;
            Threshold = setting.Threshold;
            CharacterThreshold = setting.CharacterThreshold;
            Threads = setting.Threads.Positive();
            CacheDir = cacheDir;
            var ms = ExpandingClass(setting.Model);
            if (!File.Exists(ms)) {
                throw new DirectoryNotFoundException(Properties.Resources.TW_E_ModelNotExists.Morfat(ms));
            }
            ModelPath = ms.AsFileInfo();
            LoadMetadatum();
            // ﾓﾃﾞﾙﾛーﾄﾞは途中で止められないので始める前に確認する
            token.ThrowIfCancellationRequested();
            LoadModel();
        }, token);
    }

    void LoadMetadatum() {
        if (ModelPath == null) {
            throw new ArgumentNullException(nameof(ModelPath));
        }
        FileInfo? csv;
        var tgv = ExpandingClass(setting.Tags);
        if (File.Exists(tgv)) {
            csv = tgv.AsFileInfo();
        }
        else {
            csv = ModelPath.Directory!.CombineFile(tgv);
            if (!csv.Exists) {
                throw new FileNotFoundException(Properties.Resources.TW_E_MetadataNotExists.Morfat(csv), csv.FullName);
            }
        }

        OnMessage?.Invoke(Properties.Resources.TW_LoadingTag.Morfat(csv.FullName));
        tar.Clear();
        using (var fs = csv.OpenText()) {
            var line = string.Empty;
            int index = 0;
            while ((line = fs.ReadLine()) != null) {
                if (string.IsNullOrEmpty(line)) {
                    continue;
                }
                var part = line.Split(',');
                if (part[0] == "tag_id") {
                    continue;
                }
                tar.Add(new Tag
                {
                    Index = index++,
                    TagId = long.Parse(part[0]),
                    Name = part[1],
                    Category = int.Parse(part[2]),
                    Count = long.Parse(part[3])
                });
            }
        }
        ratingIndexes = tar.Where(w => w.Category == 9).Select(w => w.Index).ToList();
        generalIndexes = tar.Where(w => w.Category == 0).Select(w => w.Index).ToList();
        characterIndexes = tar.Where(w => w.Category == 4).Select(w => w.Index).ToList();
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
                Height = meta.Value.Dimensions[1],
                Width = meta.Value.Dimensions[2]
            };
        }
        catch (Exception) {
            model?.Dispose();
            model = null;
            throw;
        }
    }

    public override TaggingResult Predict(ImageTransaction img) {
        var source = img.Bitmap;
        if (source == null) {
            throw new InvalidOperationException(Properties.Resources.TW_E_ImageIsNull);
        }
        if (model == null) {
            throw new InvalidOperationException(Properties.Resources.TW_E_ModelNotLoaded);
        }

        var input = ToTensor(source, shape.Width, shape.Height);
        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor(shape.InputName, input)
        };
        try {
            using IDisposableReadOnlyCollection<DisposableNamedOnnxValue> results = model.Run(inputs);

            var output = results.First().AsEnumerable<float>().ToArray();
            var tr = new TaggingResult();

            foreach (var i in characterIndexes) {
                if (output[i] > CharacterThreshold) {
                    tr.Character.Add(tar[i].Copy(output[i]));
                }
            }
            foreach (var i in ratingIndexes) {
                if (output[i] > Threshold) {
                    tr.Rating.Add(tar[i].Copy(output[i]));
                }
            }

            foreach (var i in generalIndexes) {
                if (output[i] > Threshold) {
                    tr.Tags.Add(tar[i].Copy(output[i]));
                }
            }
            return tr;
        }
        catch {
            throw;
        }
    }

    /// <summary>
    /// SKBitmapをｻｲｽﾞ合わせてTensorに
    /// </summary>
    /// <param name="source"></param>
    /// <param name="width"></param>
    /// <param name="height"></param>
    /// <returns></returns>
    private Tensor<float> ToTensor(SKBitmap source, int width, int height) {
        using var resized = new SKBitmap(width, height);
        using (var canvas = new SKCanvas(resized)) {
            canvas.Clear(SKColors.White);

            var scale = Math.Min((float)width / source.Width, (float)height / source.Height);
            var scaledWidth = Math.Max(1, (int)Math.Round(source.Width * scale));
            var scaledHeight = Math.Max(1, (int)Math.Round(source.Height * scale));
            var destX = (width - scaledWidth) / 2f;
            var destY = (height - scaledHeight) / 2f;

            using var paint = new SKPaint();
            paint.IsAntialias = true;

            canvas.DrawBitmap(
                source,
                new SKRect(0, 0, source.Width, source.Height),
                new SKRect(destX, destY, destX + scaledWidth, destY + scaledHeight),
                new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear),
                paint);
        }
        return ToTensor(resized);
    }

    /// <summary>
    /// SKBitpamをTensorに
    /// </summary>
    /// <param name="resized"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    private Tensor<float> ToTensor(SKBitmap resized) {
        int width = resized.Width;
        int height = resized.Height;

        var tensor = new DenseTensor<float>(
            new[] { 1, height, width, 3 });

        Span<float> dst = tensor.Buffer.Span;

        if (resized.ColorType != SKColorType.Bgra8888)
            throw new ArgumentException(Properties.Resources.TW_E_InvalidPixelFormat.Morfat(resized.ColorType));

        ReadOnlySpan<byte> span = resized.GetPixelSpan();
        int rowBytes = resized.RowBytes;

        int di = 0;

        for (int y = 0; y < height; y++) {
            int rowOffset = y * rowBytes;

            for (int x = 0; x < width; x++) {
                int p = rowOffset + x * 4;
                dst[di++] = span[p];     // B
                dst[di++] = span[p + 1]; // G
                dst[di++] = span[p + 2]; // R
            }
        }

        return tensor;
    }

}
