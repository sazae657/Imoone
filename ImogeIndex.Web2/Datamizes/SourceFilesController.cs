using ImogeIndex.Imoging;
using ImogeIndex.Sources;
using ImogeIndex.Storage;
using SkiaSharp;

namespace ImogeIndex.Web2.Datamizes;

/// <summary>
/// SourceFileとｽﾄﾘーﾑ読み込み
/// </summary>
public class SourceFilesController : IDisposable
{
    readonly DirectoryInfo work;

    public SourceFilesController()
    {
        work = Directory.CreateDirectory(
            Path.Combine(Path.GetTempPath(), $"ImogeIndexOf_{Guid.NewGuid():N}"));
    }

    public void Dispose()
    {
        try {
            work.Delete(true);
        }
        catch (IOException) {
            // 消せなかったらごめん
        }
    }

    /// <summary>
    /// 4x2のPNGを作る
    /// </summary>
    string Png(string name)
    {
        var path = Path.Combine(work.FullName, name);
        using var bitmap = new SKBitmap(4, 2);
        File.WriteAllBytes(path, bitmap.AsByteArray());
        return path;
    }

    [Fact(DisplayName = "FileInfoの属性を写し取る")]
    public void AsSourceFile()
    {
        var path = Png("a.PNG");
        var info = new FileInfo(path);
        var file = info.AsSourceFile();
        Assert.Equal(info.FullName, file.Location);
        Assert.Equal(work.FullName, file.Container);
        Assert.Equal("a.PNG", file.Name);
        Assert.Equal(".PNG", file.Extension);
        Assert.Equal(info.Length, file.Length);
        Assert.Equal(info.LastWriteTime, file.LastWriteTime);
        Assert.Equal(info.CreationTime, file.CreationTime);
    }

    [Fact(DisplayName = "作成日時が取れない供給元は更新日時で代用")]
    public void CreatedAtFallback()
    {
        var path = Png("b.png");
        var file = new FileInfo(path).AsSourceFile() with { CreationTime = null };
        var trans = FileTransaction.FromFile(LocalFileSource.Default, file);
        Assert.NotNull(trans);
        var rec = FileRecord.FromFile(trans);
        Assert.Equal(file.Location, rec.Path);
        Assert.Equal(file.Container, rec.Container);
        Assert.Equal(file.LastWriteTime, rec.CreatedAt);
        Assert.False(rec.IsStampMismatch(file));
    }

    [Fact(DisplayName = "パス、バイト列、ストリームのどれから読んでも同じ")]
    public void LoadFromAnySource()
    {
        var path = Png("c.png");
        var bytes = File.ReadAllBytes(path);
        using var fromPath = ImageLoader.Load(path);
        using var fromBytes = ImageLoader.Load(bytes);
        using var stream = new MemoryStream(bytes);
        using var fromStream = ImageLoader.Load(stream);
        foreach (var x in new[] { fromPath!, fromBytes, fromStream }) {
            Assert.Equal(4, x.Width);
            Assert.Equal(2, x.Height);
        }
    }

    [Fact(DisplayName = "ストリームからメタデータムを取れる")]
    public void AnalyzeStream()
    {
        var path = Png("d.png");
        using var stream = File.OpenRead(path);
        Assert.True(ImageMetadataAnalyzer.TryAnalyze(stream, path, out var datum, out var error));
        Assert.NotNull(datum);
        Assert.Null(error);
    }
}
