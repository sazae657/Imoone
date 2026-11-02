using SkiaSharp;
using System.CommandLine;

namespace MakeTestImage;

internal class App {
    static async Task<int> Main(string[] args) {
        var dir = new Option<string>("dir", ["--dir", "-d"])
        {
            Required = true,
        };
        var count = new Option<int>("count", ["--count", "-c"])
        {
            Required = true,
        };
        var root = new RootCommand("MakeTestImage") {
            dir, count
        };
        root.SetAction(result => {
            var d = result.GetValue(dir)!;
            var c = result.GetValue(count);
            try {
                MakeImages(d, c);
            }
            catch (Exception e) {
                Console.WriteLine($"エラー: {e.Message}\n{e.StackTrace}");
                return 17;
            }
            return 0;
        });
        var rs = root.Parse(args);
        return await rs.InvokeAsync();
    }

    static void MakeImages(string dir, int count) {
        if (!Directory.Exists(dir)) {
            throw new DirectoryNotFoundException($"{dir} がない");
        }
        if (Directory.GetFiles(dir).Length != 0 ||
            Directory.GetDirectories(dir).Length != 0) {
            throw new DirectoryNotFoundException($"{dir} が空じゃない");
        }
        var split = 1000;
        string lpart = string.Empty;
        for (int i = 0; i < count; i++) {
            var p = i / split;
            var part = $"{p * split:D8}";
            if (lpart != part) {
                Console.WriteLine($"{i}/{count}");
            }
            lpart = part;
            using var bmp = new SKBitmap(256, 256);
            using var canvas = new SKCanvas(bmp);
            canvas.Clear(CreateBCGYR((double)i/count));
            var sdf = Path.Combine(dir, part);
            if (!Directory.Exists(sdf)) {
                Directory.CreateDirectory(sdf);
            }
            var ouf = Path.Combine(sdf, $"{i:D8}.png");
            using (var fs = new FileStream(ouf, FileMode.Create)) {
                bmp.Encode(fs, SKEncodedImageFormat.Png, 80);
            }
        }
    }

    static SKColor CreateBCGYR(double indy) {
        double volume = indy;
        double pour = Math.Cos(4 * Math.PI * volume);
        int cpi = (int)((-pour / 2 + 0.5) * 255);

        var (r, g, b) = volume switch
        {
            >= 1.00 => (255, 0, 0),
            >= 0.75 => (255, cpi, 0),
            >= 0.50 => (cpi, 255, 0),
            >= 0.25 => (0, 255, cpi),
            >= 0.00 => (0, cpi, 255),
            _ => (0, 0, 255)
        };
        return new SKColor((byte)r, (byte)g, (byte)b);
    }
}
