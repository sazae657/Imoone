using ImogeIndex.Common;
using ImogeIndex.Storage;
using ImogeIndex.Sources;

namespace ImogeIndex.Web2.Datamizes;

/// <summary>
/// FileRecord
/// </summary>
public class FileRecordsController : IDisposable
{
    readonly DirectoryInfo work;

    public FileRecordsController()
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
    /// 内容とﾀｲﾑｽﾀﾝﾌﾟを指定してﾌｧｲﾙを作る
    /// </summary>
    FileInfo Imoge(string name, string body, DateTime? stamp = null)
    {
        var pth = Path.Combine(work.FullName, name);
        File.WriteAllText(pth, body);
        var fifo = new FileInfo(pth);
        if (stamp != null) {
            fifo.LastWriteTime = stamp.Value;
            fifo.Refresh();
        }
        return fifo;
    }

    /// <summary>
    /// 実ﾌｧｲﾙと一致した状態のｲﾝﾃﾞｯｸｽを作る
    /// </summary>
    static FileRecord Indexed(FileInfo fifo)
    {
        var trans = FileTransaction.FromFile(fifo);
        Assert.NotNull(trans);
        var rec = FileRecord.FromFile(trans!);
        rec.Id = 42;
        // DBは秒精度なので、読み戻した状態を再現する
        rec.ModifiedAt = DateTimeOffset.FromUnixTimeSeconds(
            fifo.LastWriteTime.AsUnixTimeSeconds()).LocalDateTime;
        return rec;
    }

    /// <summary>
    /// 解析済みの状態にする
    /// </summary>
    static FileRecord Analyzed(FileRecord rec)
    {
        rec.Version = SystemParams.VersionCurrent;
        rec.HasThumbnail = true;
        rec.HasTag = true;
        rec.Tags = "1girl,solo,nsfw";
        rec.Characters = "imoge";
        return rec;
    }

    [Fact(DisplayName = "日付とサイズが一致していれば検出しない")]
    public void IsStampMismatch1()
    {
        var fifo = Imoge("a.png", "imoge");
        var rec = Indexed(fifo);
        Assert.False(rec.IsStampMismatch(fifo.AsSourceFile()));
    }

    [Fact(DisplayName = "サイズが変わったら検出")]
    public void IsStampMismatch2()
    {
        var fifo = Imoge("a.png", "imoge");
        var rec = Indexed(fifo);
        rec.FileSize = rec.FileSize + 1;
        Assert.True(rec.IsStampMismatch(fifo.AsSourceFile()));
    }

    [Fact(DisplayName = "更新日時が変わったら検出")]
    public void IsStampMismatch3()
    {
        var fifo = Imoge("a.png", "imoge");
        var rec = Indexed(fifo);
        rec.ModifiedAt = rec.ModifiedAt.AddSeconds(-30);
        Assert.True(rec.IsStampMismatch(fifo.AsSourceFile()));
    }

    [Fact(DisplayName = "秒未満の差は検出しない (ぉ")]
    public void IsStampMismatch4()
    {
        var fifo = Imoge("a.png", "imoge", new DateTime(2024, 1, 1, 10, 0, 0, 500, DateTimeKind.Local));
        var rec = Indexed(fifo);
        Assert.Equal(new DateTime(2024, 1, 1, 10, 0, 0, DateTimeKind.Local), rec.ModifiedAt);
        Assert.False(rec.IsStampMismatch(fifo.AsSourceFile()));
    }

    [Fact(DisplayName = "中身が同じなら再解析させてやんねー")]
    public void Refresh1()
    {
        var fifo = Imoge("a.png", "imoge");
        var rec = Analyzed(Indexed(fifo));
        var hash = rec.FileHash;

        // 日付だけ動いたｹーｽ
        fifo.LastWriteTime = fifo.LastWriteTime.AddHours(1);
        fifo.Refresh();
        Assert.True(rec.IsStampMismatch(fifo.AsSourceFile()));

        Assert.False(rec.Refresh(fifo.AsSourceFile(), hash));
        Assert.False(rec.IsUpdated);
        // 日付は取り込むが解析結果は残す
        Assert.Equal(fifo.LastWriteTime, rec.ModifiedAt);
        Assert.Equal(SystemParams.VersionCurrent, rec.Version);
        Assert.True(rec.HasThumbnail);
        Assert.True(rec.HasTag);
        Assert.Equal("1girl,solo,nsfw", rec.Tags);
        Assert.False(rec.IsStampMismatch(fifo.AsSourceFile()));
    }

    [Fact(DisplayName = "中身が変わったら解析結果を捨てる")]
    public void Refresh2()
    {
        var fifo = Imoge("a.png", "imoge");
        var rec = Analyzed(Indexed(fifo));

        var beko = Imoge("a.png", "Imoone");
        var trans = FileTransaction.FromFile(beko);
        Assert.NotNull(trans);

        Assert.True(rec.Refresh(beko.AsSourceFile(), trans!.Hash!));
        Assert.True(rec.IsUpdated);
        Assert.Equal(trans.Hash, rec.FileHash);
        Assert.Equal(beko.Length, rec.FileSize);
        Assert.Equal(beko.LastWriteTime, rec.ModifiedAt);
        // ProcessImages と ProcessTagging のｽｷｯﾌﾟ条件を外す
        Assert.Equal(0, rec.Version);
        Assert.False(rec.HasThumbnail);
        Assert.False(rec.HasTag);
        Assert.Null(rec.Tags);
        Assert.Null(rec.Characters);
    }

    [Fact(DisplayName = "Idと登録日は保持する")]
    public void Refresh3()
    {
        var fifo = Imoge("a.png", "imoge");
        var rec = Analyzed(Indexed(fifo));
        var id = rec.Id;
        var registerd = rec.RegisteredAt;

        var beko = Imoge("a.png", "Imoone");
        var trans = FileTransaction.FromFile(beko);
        Assert.NotNull(trans);
        rec.Refresh(beko.AsSourceFile(), trans!.Hash!);

        // ｷｬﾌﾟｼｮﾝやｻﾑﾈｲﾙがIdで紐づいているので変わったら殺す
        Assert.Equal(id, rec.Id);
        Assert.Equal(registerd, rec.RegisteredAt);
    }

    [Fact(DisplayName = "更新後は二次判定に掛からない")]
    public void Refresh4()
    {
        var fifo = Imoge("a.png", "imoge");
        var rec = Indexed(fifo);

        var beko = Imoge("a.png", "Imoone");
        var trans = FileTransaction.FromFile(beko);
        Assert.NotNull(trans);
        rec.Refresh(beko.AsSourceFile(), trans!.Hash!);

        // 保存してあれば次のｽｷｬﾝでﾐｽﾏｯﾁにならないはず
        Assert.False(rec.IsStampMismatch(beko.AsSourceFile()));
    }

    [Fact(DisplayName = "キャプション由来の項目を全部消す")]
    public void ClearCaption1()
    {
        var rec = new FileRecord {
            Id = 7,
            HasCaption = true,
            CaptionModel = "imoge-vl",
            Lighting = "soft",
            TimeOfDay = "night",
            DominantColorPalette = "#000000",
            PeopleCount = 3,
            Keywords = "a,b",
            NotableTimestamp = "2024",
            Scene = "奈良県",
            Subjects = "ポリフェノール",
            Action = "逆立ち",
            Mood = "殺伐とした雰囲気",
            ShotType = "close-up",
            Costume = "奈良服",
            Objects = "奈良眼鏡",
            UseCases = "プロパガンタ広告",
            Tags = "1girl,solo,nsfw",
        };

        rec.ClearCaption();

        Assert.False(rec.HasCaption);
        Assert.Null(rec.CaptionModel);
        Assert.Null(rec.Lighting);
        Assert.Null(rec.TimeOfDay);
        Assert.Null(rec.DominantColorPalette);
        Assert.Equal(0, rec.PeopleCount);
        Assert.Null(rec.Keywords);
        Assert.Null(rec.NotableTimestamp);
        Assert.Null(rec.Scene);
        Assert.Null(rec.Subjects);
        Assert.Null(rec.Action);
        Assert.Null(rec.Mood);
        Assert.Null(rec.ShotType);
        Assert.Null(rec.Costume);
        Assert.Null(rec.Objects);
        Assert.Null(rec.UseCases);
        // ﾀｸﾞはｷｬﾌﾟｼｮﾝ由来ではないのでそのままのはず
        Assert.Equal("1girl,solo,nsfw", rec.Tags);
        Assert.Equal(7, rec.Id);
    }

    [Fact(DisplayName = "中身が変わるとハッシュも変われ")]
    public void FileTransaction1()
    {
        var alfa = FileTransaction.FromFile(Imoge("a.png", "imoge"));
        var beta = FileTransaction.FromFile(Imoge("b.png", "imoge"));
        var gamma = FileTransaction.FromFile(Imoge("c.png", "Imoone"));

        Assert.NotNull(alfa);
        Assert.NotNull(beta);
        Assert.NotNull(gamma);
        Assert.Equal(alfa!.Hash, beta!.Hash);
        Assert.NotEqual(alfa.Hash, gamma!.Hash);
    }
}
