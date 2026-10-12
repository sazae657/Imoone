using ImogeIndex.Common;
using ImogeIndex.Niraction.Internal;
using ImogeIndex.Web2.Common;
using System;
using System.Collections.Generic;
using System.Text;

namespace ImogeIndex.Web2;

public class NiractionTextProcessorController {
    static readonly Random rng = new();

    [Theory(DisplayName = "単純KVBSV")]
    [InlineData(1)]
    [InlineData(10)]
    [InlineData(1000)]
    [InlineData(10000)]
    public void Write(int c) {
        using var db = new DatumsBases(c);
        var fou = new FileInfo(db.Path);
        BSVPair[] bsv = [ new BSVPair(StringGen.Generate(c),StringGen.Generate(c)),
            new BSVPair("💩", "♨"),
            new BSVPair("ああああああああああ", "おおおおおおおおおおおおおお")];
        var wtr = new BSVWriter<BSVPair>();
        wtr.Values.AddRange(bsv);
        wtr.Save(fou);

        var rdr = new BSVReader<BSVPair>();
        rdr.Load(fou);
        Assert.Equal(rdr.Values, bsv);
    }

    [Theory(DisplayName = "単純KVBSV+RLE")]
    [InlineData(1)]
    [InlineData(10)]
    [InlineData(1000)]
    [InlineData(10000)]
    public void WriteRele(int c) {
        using var db = new DatumsBases(c);
        BSVPair[] bsv = [ new BSVPair(StringGen.Generate(c),StringGen.Generate(c)),
            new BSVPair("💩", "♨"),
            new BSVPair("ああああああああああ", "おおおおおおおおおおおおおお")];
        var wtr = new BSVWriter<BSVPair>();
        wtr.Values.AddRange(bsv);
        using var ms = new MemoryStream();
        wtr.Save(ms);
        ms.Seek(0, SeekOrigin.Begin);

        var fou = new FileInfo(db.Path + ".rle");
        using (var rlo = fou.OpenWrite()) {
            RleBSV.Compress(ms, rlo);
        }

        using var ims = new MemoryStream();
        using (var span = fou.OpenRead()) {
            RleBSV.Decompress(span, ims);
        }
        ims.Seek(0, SeekOrigin.Begin);

        var rdr = new BSVReader<BSVPair>();
        rdr.Load(ims);
        Assert.Equal(rdr.Values, bsv);
    }


    [Theory(DisplayName = "中型KVBSV")]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(10)]
    [InlineData(1000)]
    [InlineData(10000)]
    public void WriteLarge(int c) {
        using var db = new DatumsBases(c);
        var fou = new FileInfo(db.Path);
        var bsv = new List<BSVPair>();
        for (int i = 0; i < c; i++) {
            bsv.Add(new BSVPair($"{StringGen.Generate(100)}", $"{StringGen.Generate(100)}"));
        }

        var wtr = new BSVWriter<BSVPair>();
        wtr.Values.AddRange(bsv);
        wtr.Save(fou);

        var rdr = new BSVReader<BSVPair>();
        rdr.Load(fou);
        Assert.Equal(rdr.Values, bsv);
    }

    [Theory(DisplayName = "中型KVBSV+RLE")]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(10)]
    [InlineData(1000)]
    [InlineData(10000)]
    public void WriteLargeRele(int c) {
        using var db = new DatumsBases(c);
        var bsv = new List<BSVPair>();
        for (int i = 0; i < c; i++) {
            bsv.Add(new BSVPair($"{StringGen.Generate(100)}", $"{StringGen.Generate(100)}"));
        }
        var wtr = new BSVWriter<BSVPair>();
        wtr.Values.AddRange(bsv);
        using var ms = new MemoryStream();
        wtr.Save(ms);
        ms.Seek(0, SeekOrigin.Begin);

        var fou = new FileInfo(db.Path + ".rle");
        using (var rlo = fou.OpenWrite()) {
            RleBSV.Compress(ms, rlo);
        }

        using var ims = new MemoryStream();
        using (var span = fou.OpenRead()) {
            RleBSV.Decompress(span, ims);
        }
        ims.Seek(0, SeekOrigin.Begin);

        var rdr = new BSVReader<BSVPair>();
        rdr.Load(ims);
        Assert.Equal(rdr.Values, bsv);
    }


    [Theory(DisplayName = "大型RCBSV")]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(10)]
    [InlineData(1000)]
    [InlineData(10000)]
    public void WriteLargeMul(int c) {
        using var db = new DatumsBases(c);
        var fou = new FileInfo(db.Path);
        var bsv = new List<BSVRow>();
        for (int i = 0; i < c; i++) {
            bsv.Add(new BSVRow(Enumerable.Range(0, rng.Next(1, 16)).Select(_ => StringGen.Generate(100))));
        }

        var wtr = new BSVWriter<BSVRow>();
        wtr.Values.AddRange(bsv);
        wtr.Save(fou);

        var rdr = new BSVReader<BSVRow>();
        rdr.Load(fou);
        Assert.Equal(rdr.Values, bsv);
    }

    [Theory(DisplayName = "大型RCBSV+RLE")]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(10)]
    [InlineData(1000)]
    [InlineData(10000)]
    public void WriteLargeReleMul(int c) {
        using var db = new DatumsBases(c);
        var bsv = new List<BSVRow>();
        for (int i = 0; i < c; i++) {
            bsv.Add(new BSVRow(Enumerable.Range(0, rng.Next(1, 16)).Select(_ => StringGen.Generate(100))));
        }
        var wtr = new BSVWriter<BSVRow>();
        wtr.Values.AddRange(bsv);
        using var ms = new MemoryStream();
        wtr.Save(ms);
        ms.Seek(0, SeekOrigin.Begin);

        var fou = new FileInfo(db.Path + ".rle");
        using (var rlo = fou.OpenWrite()) {
            RleBSV.Compress(ms, rlo);
        }

        using var ims = new MemoryStream();
        using (var span = fou.OpenRead()) {
            RleBSV.Decompress(span, ims);
        }
        ims.Seek(0, SeekOrigin.Begin);

        var rdr = new BSVReader<BSVRow>();
        rdr.Load(ims);
        Assert.Equal(rdr.Values, bsv);
    }

    sealed class DatumsBases : IDisposable {
        public DatumsBases(int cc) {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"bsv-{Guid.NewGuid():N}_{cc}.bsv");
        }

        public string Path { get; }

        public void Dispose() {
            foreach (var suffix in new[] { "", ".rle" }) {
                File.Delete(Path + suffix);
            }
        }
    }
}
