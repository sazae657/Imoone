using ImogeIndex.Storage;
using ImogeIndex.Taggers;
using ImogeIndex.Sources;
using System.Data.SQLite;
using System.Reflection;

namespace ImogeIndex.Web2.Datamizes;

/// <summary>
/// ﾀｸﾞ保存の保持・置換・無効化を確認する
/// </summary>
[Collection("プロファイル")]
public class TagRecordsController : IDisposable
{
    readonly TestIndexDatum db = new();
    readonly DirectoryInfo work = Directory.CreateTempSubdirectory("imoge-tags-");
    readonly FieldInfo thumbnailField = typeof(ThumbnailStore).GetField("instance", BindingFlags.Static | BindingFlags.NonPublic)!;
    readonly object? previousThumbnail;
    readonly ThumbnailStore thumbnails;
    readonly FileInfo image;

    public TagRecordsController()
    {
        previousThumbnail = thumbnailField.GetValue(null);
        thumbnails = (ThumbnailStore)Activator.CreateInstance(typeof(ThumbnailStore), true)!;
        typeof(DataStoreBase).GetMethod("OpenConnection", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(thumbnails, [new FileInfo(Path.Combine(work.FullName, "thumb.sqlite")), true]);
        thumbnailField.SetValue(null, thumbnails);
        image = new FileInfo(Path.Combine(work.FullName, "image.png"));
        File.WriteAllText(image.FullName, "original");
        var file = Assert.Single(db.Store.AddFiles([FileTransaction.FromFile(image)!], CancellationToken.None));
        SaveTags(file, "original", 0.75f);
    }

    [Fact(DisplayName = "日時だけの更新ではスコア付きタグを保持する")]
    public void KeepsTagsWhenOnlyTimestampChanges()
    {
        var file = Assert.Single(db.Store.QueryAll(CancellationToken.None));
        Assert.Null(file.TaggingResult);
        var original = ReadTags();
        image.LastWriteTime = image.LastWriteTime.AddHours(1);
        image.Refresh();
        Assert.False(file.Refresh(image.AsSourceFile(), file.FileHash));
        db.Store.Update([file], false, CancellationToken.None);
        Assert.Equal(original, ReadTags());
        Assert.True(Assert.Single(db.Store.QueryAll(CancellationToken.None)).HasTag);
    }

    [Fact(DisplayName = "再タグ付けでは旧タグを新しいタグとスコアに置換する")]
    public void ReplacesTagsWhenRetagged()
    {
        var file = Assert.Single(db.Store.QueryAll(CancellationToken.None));
        SaveTags(file, "replacement", 0.5f);
        Assert.Equal(new[] {
            ("Character", "replacement-character", 0.5),
            ("Rating", "replacement-rating", 0.5),
            ("Tags", "replacement", 0.5),
        }, ReadTags());
    }

    [Fact(DisplayName = "画像内容の変更ではスコア付きタグと未保存のタグ結果を無効化する")]
    public void DeletesTagsWhenContentChanges()
    {
        var file = Assert.Single(db.Store.QueryAll(CancellationToken.None));
        // 同じﾚｺーﾄﾞに前回の結果が残っていても保存し直さない
        file.TaggingResult = new TaggingResult();
        file.TaggingResult.Tags.Add(new Tag { Name = "stale", Score = 0.5f });
        File.WriteAllText(image.FullName, "changed");
        image.Refresh();
        Assert.True(file.Refresh(image.AsSourceFile(), FileTransaction.FromFile(image)!.Hash!));
        Assert.Null(file.TaggingResult);
        db.Store.Update([file], false, CancellationToken.None);
        Assert.Empty(ReadTags());
        Assert.False(Assert.Single(db.Store.QueryAll(CancellationToken.None)).HasTag);
    }

    [Fact(DisplayName = "保存が確定したらタグ結果を手放す")]
    public void ReleasesTaggingResultAfterCommit()
    {
        var file = Assert.Single(db.Store.QueryAll(CancellationToken.None));
        SaveTags(file, "released", 0.5f);
        Assert.Null(file.TaggingResult);
        Assert.Equal(3, ReadTags().Length);
    }

    [Fact(DisplayName = "保存が中断されたらタグ結果を保持する")]
    public void KeepsTaggingResultWhenCancelled()
    {
        var file = Assert.Single(db.Store.QueryAll(CancellationToken.None));
        file.TaggingResult = new TaggingResult();
        file.TaggingResult.Tags.Add(new Tag { Name = "pending", Score = 0.5f });
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        db.Store.Update([file], false, cts.Token);
        Assert.NotNull(file.TaggingResult);
    }

    void SaveTags(FileRecord file, string name, float score)
    {
        file.HasTag = true;
        file.Tags = name;
        file.Characters = name + "-character";
        file.TaggingResult = new TaggingResult();
        file.TaggingResult.Tags.Add(new Tag { Name = name, Score = score });
        file.TaggingResult.Character.Add(new Tag { Name = file.Characters, Score = score });
        file.TaggingResult.Rating.Add(new Tag { Name = name + "-rating", Score = score });
        db.Store.Update([file], false, CancellationToken.None);
    }

    (string Type, string Tag, double Score)[] ReadTags()
    {
        using var command = new SQLiteCommand("SELECT type, tag, score FROM tags ORDER BY type, tag;", db.Connection);
        using var reader = command.ExecuteReader();
        var tags = new List<(string, string, double)>();
        while (reader.Read()) {
            tags.Add((reader.GetString(0), reader.GetString(1), reader.GetDouble(2)));
        }
        return tags.ToArray();
    }

    public void Dispose()
    {
        thumbnailField.SetValue(null, previousThumbnail);
        thumbnails.Dispose();
        db.Dispose();
        work.Delete(true);
    }
}
