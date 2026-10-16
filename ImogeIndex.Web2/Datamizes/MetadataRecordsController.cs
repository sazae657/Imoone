using ImogeIndex.Imoging;
using ImogeIndex.Storage;
using System.Data.SQLite;
using System.Reflection;

namespace ImogeIndex.Web2.Datamizes;

/// <summary>
/// ﾒﾀﾃﾞーﾀﾑ保存後の後始末を確認する
/// </summary>
[Collection("プロファイル")]
public class MetadataRecordsController : IDisposable
{
    readonly TestIndexDatum db = new();
    readonly DirectoryInfo work = Directory.CreateTempSubdirectory("imoge-metadata-");
    readonly FieldInfo thumbnailField = typeof(ThumbnailStore).GetField("instance", BindingFlags.Static | BindingFlags.NonPublic)!;
    readonly object? previousThumbnail;
    readonly ThumbnailStore thumbnails;
    readonly FileRecord file;

    public MetadataRecordsController()
    {
        previousThumbnail = thumbnailField.GetValue(null);
        thumbnails = (ThumbnailStore)Activator.CreateInstance(typeof(ThumbnailStore), true)!;
        typeof(DataStoreBase).GetMethod("OpenConnection", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(thumbnails, [new FileInfo(Path.Combine(work.FullName, "thumb.sqlite")), true]);
        thumbnailField.SetValue(null, thumbnails);
        var image = new FileInfo(Path.Combine(work.FullName, "image.png"));
        File.WriteAllText(image.FullName, "original");
        file = Assert.Single(db.Store.AddFiles([FileTransaction.FromFile(image)!], CancellationToken.None));
    }

    [Fact(DisplayName = "保存が確定したらメタデータムを手放す")]
    public void ReleasesMetadataAfterCommit()
    {
        SetMetadata();
        db.Store.Update([file], true, CancellationToken.None);
        Assert.Null(file.MetadataDatum);
        Assert.False(file.MetadataChanged);
        Assert.Equal(1, CountMetadata());
    }

    [Fact(DisplayName = "メタデータムを書かない保存では保持する")]
    public void KeepsMetadataWhenNotOptional()
    {
        SetMetadata();
        db.Store.Update([file], false, CancellationToken.None);
        Assert.NotNull(file.MetadataDatum);
        Assert.True(file.MetadataChanged);
        Assert.Equal(0, CountMetadata());
    }

    [Fact(DisplayName = "保存が中断されたらメタデータムを保持する")]
    public void KeepsMetadataWhenCancelled()
    {
        SetMetadata();
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        db.Store.Update([file], true, cts.Token);
        Assert.NotNull(file.MetadataDatum);
        Assert.True(file.MetadataChanged);
    }

    void SetMetadata()
    {
        file.MetadataDatum = new ImageMetadataDatum { FileId = file.Id };
        file.MetadataDatum.Tags.Add(new ImageMetadataValue {
            Type = "Exif",
            Directory = "Exif IFD0",
            Tag = "Model",
            Value = "Test",
        });
        file.MetadataChanged = true;
    }

    long CountMetadata()
    {
        using var command = new SQLiteCommand("SELECT COUNT(*) FROM metadata WHERE image_id=@id;", db.Connection);
        command.Parameters.AddWithValue("@id", file.Id);
        return (long)command.ExecuteScalar();
    }

    public void Dispose()
    {
        thumbnailField.SetValue(null, previousThumbnail);
        thumbnails.Dispose();
        db.Dispose();
        work.Delete(true);
    }
}
