using ImogeIndex.Captionizers;
using ImogeIndex.Common;
using ImogeIndex.Imoging;
using ImogeIndex.Sources;
using System.Data.SQLite;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Tomlet;

namespace ImogeIndex.Storage;


/// <summary>
/// ﾌｧｲﾙ処理用のﾄﾗﾝｻﾞｸｼｮﾝ
/// </summary>
public class FileTransaction
{
    public SourceFile? File { get; set; }
    public string? Hash { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }

    public static FileTransaction? FromFile(IFileSource source, SourceFile file)
    {
        try {
            using (var fo = source.OpenRead(file.Location)) {
                var hash = SHA512.HashData(fo);
                return new FileTransaction
                {
                    File = file,
                    Hash = Convert.ToHexString(hash).ToLowerInvariant()
                };
            }
        }
        catch(Exception r) {
            Logger.Dictator.Error(r, file.Location);
        }
        return null;
    }

    /// <summary>
    /// ﾛーｶﾙﾌｧｲﾙから(主にﾃｽﾄ用)
    /// </summary>
    /// <param name="path"></param>
    /// <returns></returns>
    public static FileTransaction? FromFile(FileInfo path)
    {
        SourceFile file;
        try {
            // 実在しないとLengthで例外が出る
            file = path.AsSourceFile();
        }
        catch (Exception r) {
            Logger.Dictator.Error(r, path.FullName);
            return null;
        }
        return FromFile(LocalFileSource.Default, file);
    }
}

/// <summary>
/// ｲﾝﾃﾞｯｸｽ
/// </summary>
[MurTable("imoge_index")]
public class FileRecord {
    [MurColumn("id", MurColumnFlags.PrimaryKey)]
    public long Id { get; set; }
    [MurColumn("path", MurColumnFlags.NotNull)]
    public string Path { get; set; } = string.Empty;

    [MurColumn("container", MurColumnFlags.NotNull)]
    public string Container { get; set; } = string.Empty;

    [MurColumn("file_hash", MurColumnFlags.NotNull)]
    public string FileHash { get; set; } = string.Empty;

    [MurColumn("version")]
    public int Version { get; set; }
    [MurColumn("registered_at", MurColumnFlags.NotNull)]
    public DateTime RegisteredAt { get; set; }
    [MurColumn("created_at", MurColumnFlags.NotNull)]
    public DateTime CreatedAt { get; set; }
    [MurColumn("modified_at", MurColumnFlags.NotNull)]
    public DateTime ModifiedAt { get; set; }

    [MurColumn("file_size", MurColumnFlags.NotNull)]
    public long FileSize { get; set; } = 0;

    [MurColumn("width", MurColumnFlags.NotNull)]
    public int Width { get; set; }

    [MurColumn("height", MurColumnFlags.NotNull)]
    public int Height { get; set; }

    [MurColumn("phash")]
    public ulong PHash { get; set; }
    [MurColumn("ahash")]
    public ulong AHash { get; set; }
    [MurColumn("dhash")]
    public ulong DHash { get; set; }
    [MurColumn("whash")]
    public ulong WHash { get; set; }

    [MurColumn("has_thumbnail")]
    public bool HasThumbnail { get; set; } = false;

    [MurColumn("has_tag")]
    public bool HasTag { get; set; } = false;

    [MurColumn("characters")]
    public string? Characters { get; set; }
    [MurColumn("tags")]
    public string? Tags { get; set; }

    [MurColumn("has_caption")]
    public bool HasCaption { get; set; } = false;

    [MurColumn("caption_model")]
    public string? CaptionModel { get; set; }

    [MurColumn("lighting")]
    public string? Lighting { get; set; }

    [MurColumn("time_of_day")]
    public string? TimeOfDay { get; set; }

    [MurColumn("dominant_color_palette")]
    public string? DominantColorPalette { get; set; }
    [MurColumn("people_count")]
    public int PeopleCount { get; set; }
    [MurColumn("keywords")]
    public string? Keywords { get; set; }
    [MurColumn("notable_timestamp")]
    public string? NotableTimestamp { get; set; }

    [MurColumn("scene")]
    public string? Scene { get; set; }
    [MurColumn("subjects")]
    public string? Subjects { get; set; }

    [MurColumn("action")]
    public string? Action { get; set; }
    [MurColumn("mood")]
    public string? Mood { get; set; }
    [MurColumn("shot_type")]
    public string? ShotType { get; set; }
    
    [MurColumn("costume")]
    public string? Costume { get; set; }

    [MurColumn("objects")]
    public string? Objects { get; set; }
    [MurColumn("use_cases")]
    public string? UseCases { get; set; }


    [MurColumn("pending_recaption")]
    public bool PendingRecaption { get; set; }

    // DBに入れない
    public bool IsAlive { get; set; } = false;
    public bool CaptionInvalidationReady { get; set; }

    
    // ﾌｧｲﾙが差し替わったので再解析
    public bool IsUpdated { get; set; } = false;
    public byte[]? Thumbnail { get; set; } = null;
    public ImageMetadataDatum? MetadataDatum { get; set; } = null;

    /// <summary>
    /// ﾒﾀﾃﾞーﾀﾑを取り直したので保存時に書き直す
    /// </summary>
    public bool MetadataChanged { get; set; } = false;

    public Taggers.TaggingResult? TaggingResult { get; set; } = null;

    /// <summary>
    /// 実ﾌｧｲﾙとﾀｲﾑｽﾀﾝﾌﾟorｻｲｽﾞが食い違ってる？(更新の一次判定)
    /// </summary>
    /// <param name="info"></param>
    /// <returns></returns>
    public bool IsStampMismatch(SourceFile info) {
        // modified_atは秒精度ぬわので、比較も秒に丸める
        return info.Length != FileSize
            || info.LastWriteTime.AsUnixTimeSeconds() != ModifiedAt.AsUnixTimeSeconds();
    }

    /// <summary>
    /// 実ﾌｧｲﾙの情報を取り込む
    /// 中身が変わっていれば解析結果を捨てて再解析させる
    /// </summary>
    /// <param name="info"></param>
    /// <param name="hash"></param>
    /// <returns></returns>
    public bool Refresh(SourceFile info, string hash) {
        var changed = hash != FileHash;
        // 中身が同じでも日付とｻｲｽﾞは書き戻す
        FileHash = hash;
        FileSize = info.Length;
        ModifiedAt = info.LastWriteTime;
        if (!changed) {
            return false;
        }
        IsUpdated = true;
        // 画像解析とﾀｸﾞ付けをやり直させる
        Version = 0;
        HasThumbnail = false;
        HasTag = false;
        TaggingResult = null;
        Tags = null;
        Characters = null;
        return true; // 中身が変わってる
    }

    /// <summary>
    /// ｷｬﾌﾟｼｮﾝ由来の項目を消す
    /// </summary>
    public void ClearCaption() {
        HasCaption = false;
        CaptionModel = null;
        Lighting = null;
        TimeOfDay = null;
        DominantColorPalette = null;
        PeopleCount = 0;
        Keywords = null;
        NotableTimestamp = null;
        Scene = null;
        Subjects = null;
        Action = null;
        Mood = null;
        ShotType = null;
        Costume = null;
        Objects = null;
        UseCases = null;
    }

    /// <summary>
    /// 読み込みﾄﾗﾝｻﾞｸｼｮﾝから生成
    /// </summary>
    /// <param name="file"></param>
    /// <returns></returns>
    public static FileRecord FromFile(FileTransaction file) {
        return new FileRecord {
            Id = -1,
            Path = file.File!.Location,
            Container = file.File!.Container,
            Version = 0,
            RegisteredAt = DateTime.Now,
            // 作成日時が取れない供給元は更新日時で代用
            CreatedAt = file.File!.CreationTime ?? file.File!.LastWriteTime,
            ModifiedAt = file.File!.LastWriteTime,
            FileSize = file.File!.Length,
            FileHash = file.Hash!,
            Width = file.Width,
            Height = file.Height
        };
    }
}

/// <summary>
/// ｴﾗー扱いにした原因
/// </summary>
public enum ErrorType
{
    None,
    File,
    Caption,
}

/// <summary>
/// ｴﾗーﾌﾗｸﾞ
/// </summary>
[MurTable("errors")]
public class ErrorRecord
{
    [MurColumn("id", MurColumnFlags.PrimaryKey)]
    public long Id { get; set; }

    [MurColumn("error_type")]
    public ErrorType ErrorType { get; set; } = ErrorType.None;
}

/// <summary>
/// Taggerのﾀｸﾞそのまんま(そのうち使う)
/// </summary>
[MurTable("tags")]
public class TagRecord {
    [MurColumn("image_id")]
    public long ImageId { get; set; }

    [MurColumn("type")]
    public string? Type { get; set; }

    [MurColumn("tag")]
    public string? Tag { get; set; }

    [MurColumn("score")]
    public double Score { get; set; }
}

/// <summary>
/// LLMから受信したｷｬﾌﾟｼｮﾝそのまんま
/// </summary>
[MurTable("captions")]
public class CaptionRecord {
    [MurColumn("id", MurColumnFlags.PrimaryKey)]
    public long Id { get; set; }
    
    [MurColumn("model")]
    public string? Model { get; set; }

    [MurColumn("caption")]
    public string? Image { get; set; }
}

/// <summary>
/// 画像のﾒﾀﾃﾞーﾀﾑ
/// </summary>
[MurTable("metadata")]
public class MetadataRecord {
    [MurColumn("image_id")]
    public long ImageId { get; set; }
    
    [MurColumn("type")]
    public string? Type { get; set; }

    [MurColumn("directory")]
    public string? Directory { get; set; }

    [MurColumn("tag")]
    public string? Tag { get; set; }

    [MurColumn("value")]
    public string? Value { get; set; }

    [MurColumn("numeric_value")]
    public double? NumericValue { get; set; }
}

/// <summary>
/// ﾒﾀﾃﾞーﾀﾑ検索ｲﾝﾃﾞｯｸｽの世代情報
/// </summary>
public static class MetadataIndexMarker {
    /// <summary>
    /// ｴｲﾘｱｽのﾊｯｼｭ作成
    /// </summary>
    /// <param name="tags"></param>
    /// <returns></returns>
    public static long CalculateTagHash(IEnumerable<MetaAlias> tags) {
        var normalized = string.Join("\n", tags
            .Where(x => !string.IsNullOrWhiteSpace(x.Alias) && !string.IsNullOrWhiteSpace(x.Tag))
            .Select(x => $"{x.Alias.Trim().ToLowerInvariant()}:{x.ValueType}={x.Tag.Trim().ToLowerInvariant()}")
            .Distinct(StringComparer.Ordinal)
            .OrderBy(x => x, StringComparer.Ordinal));
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
        return BitConverter.ToInt64(hash, 0);
    }
}

/// <summary>
/// ﾃﾞｨﾚｸﾄﾘ情報(主にﾀｲﾑｽﾀﾝﾌﾟ)
/// </summary>
[MurTable("directory_index")]
public class DirectoryIndexRecord
{
    [MurColumn("path", MurColumnFlags.PrimaryKey | MurColumnFlags.NotNull)]
    public string Path { get; set; } = string.Empty;

    [MurColumn("last_modified")]
    public long LastModified { get; set; }

    public bool IsAlive { get; set; } = false;
}

/// <summary>
/// ﾒｲﾝのｲﾝﾃﾞｯｸｽ
/// </summary>
public partial class IndexStore : DataStoreBase
{
    static IndexStore? instance;

    private bool disposedValue;

    long deleteCount = 0;

    const long databaseVersion = 1;

    public static IndexStore Instance =>
        instance ?? throw new InvalidOperationException(Properties.Resources.SM_E_DatumNotOpenEx.Morfat(nameof(IndexStore)));

    /// <summary>
    /// 開いてる？
    /// </summary>
    public static bool IsOpened => instance != null;


    IndexStore() { }

    /// <summary>
    /// 開く
    /// </summary>
    /// <param name="writable"></param>
    public static void Open(bool writable) {
        if (instance != null) {
            return;
        }
        var dbp = Profile.Active.GetIndexDatumPath();
        var store = new IndexStore();
        try {
            store.OpenConnection(dbp, writable);
        }
        catch {
            // 開けなかったものはｼﾝｸﾞﾙﾄﾝに残さない
            store.Dispose();
            throw;
        }
        if (Interlocked.CompareExchange(ref instance, store, null) != null) {
            // 別ｽﾚｯﾄﾞが先に開いた
            store.Dispose();
        }
    }

    /// <summary>
    /// ﾃﾞｰﾀﾑある？
    /// </summary>
    /// <returns></returns>
    public static bool DatumExists() {
        var dbp = Profile.Active.GetIndexDatumPath();
        return dbp.Exists;
    }

    /// <summary>
    /// とじこ
    /// </summary>
    public static void Close() {
        Interlocked.Exchange(ref instance, null)?.Dispose();
    }

    /// <summary>
    /// とじこして開く
    /// </summary>
    /// <param name="writable"></param>
    public static void Oppen(bool writable)
    {
        Close();
        Open(writable);
    }

    /// <summary>
    /// ﾌｧｲﾙ情報一括追加
    /// </summary>
    /// <param name="files"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException"></exception>
    public IEnumerable<FileRecord> AddFiles(IEnumerable<FileTransaction> files, CancellationToken token) {
        if (!CanWrite) {
            throw new ArgumentNullException(Properties.Resources.SM_E_NotOpenOrReadOnlyEx.Morfat(nameof(IndexStore)));
        }
        long iid = 0;
        List<FileRecord> rpm = new();
        var sql = """
            INSERT INTO imoge_index (id, path,container, file_hash, version, registered_at, created_at, modified_at, file_size, width, height)
            VALUES (@id, @path, @container, @file_hash, @version, @registered_at, @created_at, @modified_at, @file_size, @width, @height)
            """;
        var committed = TransactionOf(sql, (command, transaction) => {
            // 採番は書き込みﾄﾗﾝｻﾞｸｼｮﾝの中で読む
            iid = GetLastCounting(transaction);
            command.Parameters.Add("@id", System.Data.DbType.Int64);
            command.Parameters.Add("@path", System.Data.DbType.String);
            command.Parameters.Add("@container", System.Data.DbType.String);
            command.Parameters.Add("@file_hash", System.Data.DbType.String);
            command.Parameters.Add("@version", System.Data.DbType.Int32);
            command.Parameters.Add("@registered_at", System.Data.DbType.Int64);
            command.Parameters.Add("@created_at", System.Data.DbType.Int64);
            command.Parameters.Add("@modified_at", System.Data.DbType.Int64);
            command.Parameters.Add("@file_size", System.Data.DbType.Int64);
            command.Parameters.Add("@width", System.Data.DbType.Int32);
            command.Parameters.Add("@height", System.Data.DbType.Int32);
            int n = 0;
            foreach (var x in files) {
                if (token.IsCancellationRequested) {
                    return;
                }
                n++;
                var fsm = FileRecord.FromFile(x);
                fsm.Id = iid++;
                command.Parameters["@id"].Value = fsm.Id;
                command.Parameters["@path"].Value = fsm.Path;
                command.Parameters["@container"].Value = fsm.Container;
                command.Parameters["@file_hash"].Value = fsm.FileHash;
                command.Parameters["@version"].Value = fsm.Version;
                command.Parameters["@registered_at"].Value = fsm.RegisteredAt.AsUnixTimeSeconds();
                command.Parameters["@created_at"].Value = fsm.CreatedAt.AsUnixTimeSeconds();
                command.Parameters["@modified_at"].Value = fsm.ModifiedAt.AsUnixTimeSeconds();
                command.Parameters["@width"].Value = fsm.Width;
                command.Parameters["@file_size"].Value = fsm.FileSize;
                command.Parameters["@height"].Value = fsm.Height;
                command.ExecuteNonQuery();
                rpm.Add(fsm);
            }
            // 採番ｶｳﾝﾀーは同じﾄﾗﾝｻﾞｸｼｮﾝで進め、ﾛーﾙﾊﾞｯｸ時に一緒に戻す
            using var counting = new SQLiteCommand("UPDATE counting SET count=@count;", transaction.Connection, transaction);
            counting.Parameters.AddWithValue("@count", iid);
            counting.ExecuteNonQuery();
        }, token);
        if (!committed) {
            // ﾛーﾙﾊﾞｯｸされたのでDBに無いﾚｺーﾄﾞは返さない
            return [];
        }
        return rpm;
    }

    /// <summary>
    /// ﾌｧｲﾙﾚｺーﾄﾞ更新
    /// </summary>
    /// <param name="files"></param>
    /// <param name="optional"></param>
    /// <param name="token"></param>
    /// <exception cref="ArgumentNullException"></exception>
    /// <exception cref="Exception"></exception>
    /// <returns></returns>
    public int Update(IEnumerable<FileRecord> files, bool optional, CancellationToken token)
    {
        if (!CanWrite) {
            throw new ArgumentNullException(Properties.Resources.SM_E_NotOpenOrReadOnlyEx.Morfat(nameof(IndexStore)));
        }
        if (!ThumbnailStore.IsOpened) {
            throw new ArgumentNullException(Properties.Resources.SM_E_DatumNotOpenEx.Morfat(nameof(ThumbnailStore)));
        }

        var cols = ColumnNamesOf<FileRecord>().Where(w => w != "id").ToList();
        var era = 0;
        var committed = TransactionOf(transaction => {
            var apql = new StringBuilder();
            apql.Append("UPDATE imoge_index SET ")
                .Append(string.Join(", ", cols.Select(w => $"{w}=@{w}")))
                .Append(" WHERE id=@id;");
            using var command = new SQLiteCommand(apql.ToString(), transaction.Connection, transaction);
            PrepareCommand<FileRecord>(command);
            // ﾚｺーﾄﾞごとに作り直すと数万件で重いので使い回す
            using var delete = new SQLiteCommand("DELETE FROM captions WHERE id=@id;", transaction.Connection, transaction);
            delete.Parameters.Add("@id", System.Data.DbType.Int64);
            using var deltag = new SQLiteCommand("DELETE FROM tags WHERE image_id=@image_id;", transaction.Connection, transaction);
            deltag.Parameters.Add("@image_id", System.Data.DbType.Int64);
            using var shaver = new SQLiteCommand(
                "INSERT INTO tags (image_id, type, tag, score) VALUES(@image_id, @type, @tag, @score);", transaction.Connection, transaction);
            shaver.Parameters.Add("@image_id", System.Data.DbType.Int64);
            shaver.Parameters.Add("@type", System.Data.DbType.String);
            shaver.Parameters.Add("@tag", System.Data.DbType.String);
            shaver.Parameters.Add("@score", System.Data.DbType.Double);
            int n = 0;
            foreach (var x in files) {
                if (token.IsCancellationRequested) {
                    return;
                }
                n++;
                if (x.CaptionInvalidationReady && x.PendingRecaption) {
                    // 更新かつ再ｷｬﾌﾟｼｮﾝならｷｬﾌﾟｼｮﾝ削除
                    delete.Parameters["@id"].Value = x.Id;
                    delete.ExecuteNonQuery();
                    x.ClearCaption();
                    x.PendingRecaption = false;
                }
                MapRow(x, command);
                era += command.ExecuteNonQuery();

                if (x.TaggingResult != null || !x.HasTag) {
                    // 再ﾀｸﾞ付けまたは解析結果の無効化時だけ旧ﾀｸﾞを削除する
                    deltag.Parameters["@image_id"].Value = x.Id;
                    deltag.ExecuteNonQuery();
                }

                if (x.TaggingResult != null) {
                    // ﾀｸﾞ保存
                    void InsTag(string type, IEnumerable<Taggers.Tag> ipv7) {
                        foreach (var m in ipv7) {
                            shaver.Parameters["@image_id"].Value = x.Id;
                            shaver.Parameters["@type"].Value = type;
                            shaver.Parameters["@tag"].Value = m.Name;
                            shaver.Parameters["@score"].Value = (double)m.Score;
                            era += shaver.ExecuteNonQuery();
                        }
                    }

                    InsTag(nameof(Taggers.TaggingResult.Character), x.TaggingResult.Character.Where(w => !string.IsNullOrEmpty(w.Name)));
                    InsTag(nameof(Taggers.TaggingResult.Rating), x.TaggingResult.Rating.Where(w => !string.IsNullOrEmpty(w.Name)));
                    InsTag(nameof(Taggers.TaggingResult.Tags), x.TaggingResult.Tags.Where(w => !string.IsNullOrEmpty(w.Name)));
                }

            }
            Debug.WriteLine($"COMMIT {n}");

            if (!optional) {
                return;
            }

            var swql = $"INSERT INTO metadata (image_id, type, directory, tag, value, numeric_value) VALUES(@image_id, @type, @directory, @tag, @value, @numeric_value);";
            // ここで削除と再挿入を同時に確定して中断時には既存のﾒﾀﾃﾞーﾀを残せれば…いいな
            using var deleteMetadata = new SQLiteCommand("DELETE FROM metadata WHERE image_id=@image_id;", transaction.Connection, transaction);
            deleteMetadata.Parameters.Add("@image_id", System.Data.DbType.Int64);
            using var command2 = new SQLiteCommand(swql, transaction.Connection, transaction);
            command2.Parameters.Add("@image_id", System.Data.DbType.Int64);
            command2.Parameters.Add("@type", System.Data.DbType.String);
            command2.Parameters.Add("@directory", System.Data.DbType.String);
            command2.Parameters.Add("@tag", System.Data.DbType.String);
            command2.Parameters.Add("@value", System.Data.DbType.String);
            command2.Parameters.Add("@numeric_value", System.Data.DbType.Double);
            foreach (var x in files) {
                if (token.IsCancellationRequested) {
                    return;
                }
                if (!x.MetadataChanged) {
                    continue;
                }
                if (x.MetadataDatum == null) {
                    Logger.Dictator.Warn($"メタデータム無しで書き直そうとしたので無視: {x.Path}");
                    continue;
                }
                deleteMetadata.Parameters["@image_id"].Value = x.Id;
                deleteMetadata.ExecuteNonQuery();
                foreach (var m in x.MetadataDatum.Tags) {
                    command2.Parameters["@image_id"].Value = x.Id;
                    command2.Parameters["@type"].Value = m.Type;
                    command2.Parameters["@directory"].Value = m.Directory;
                    command2.Parameters["@tag"].Value = m.Tag;
                    command2.Parameters["@value"].Value = m.Value;
                    command2.Parameters["@numeric_value"].Value = (object?)m.NumericValue ?? DBNull.Value;
                    command2.ExecuteNonQuery();
                }
            }
        }, token);
        if (committed) {
            // 確定したので解析結果は不要、全件抱えるとﾒﾓﾘを食い潰すので捨てる
            foreach (var x in files) {
                x.TaggingResult = null;
                if (optional) {
                    // 次の保存では書き直さない
                    x.MetadataChanged = false;
                    x.MetadataDatum = null;
                }
            }
        }
        if (!optional) {
            return era;
        }
        ThumbnailStore.Instance.Set(files, token);
        return era;
    }

    /// <summary>
    /// ﾌｧｲﾙﾚｺーﾄﾞ更新
    /// </summary>
    /// <param name="file"></param>
    /// <param name="token"></param>
    /// <exception cref="Exception"></exception>
    public void Update(FileRecord file, CancellationToken token) {
        if (!CanWrite) {
            return;
        }
        var sql = new StringBuilder();
        var cols = ColumnNamesOf<FileRecord>().Where(w => w != "id").ToList();
        sql.Append("UPDATE imoge_index SET ")
            .Append(string.Join(", ", cols.Select(w => $"{w}=@{w}")))
            .Append(" WHERE id=@id;");
        TransactionOf(transaction => {
            using var cmd = new SQLiteCommand(sql.ToString(), transaction.Connection, transaction);
            PrepareCommand<FileRecord>(cmd);
            MapRow(file, cmd);
            cmd.ExecuteNonQuery();
        }, token);
    }

    /// <summary>
    /// ﾌｧｲﾙﾚｺーﾄﾞと関連ﾚｺーﾄﾞ一括削除
    /// </summary>
    /// <param name="files"></param>
    /// <param name="token"></param>
    /// <exception cref="ArgumentNullException"></exception>
    /// <returns>消した件数</returns>
    public int Purge(IEnumerable<FileRecord> files, CancellationToken token) {
        if (!CanWrite) {
            throw new ArgumentNullException(Properties.Resources.SM_E_NotOpenOrReadOnlyEx.Morfat(nameof(IndexStore)));
        }
        if (!ThumbnailStore.IsOpened) {
            throw new ArgumentNullException(Properties.Resources.SM_E_DatumNotOpenEx.Morfat(nameof(ThumbnailStore)));
        }

        // 消すﾃーﾌﾞﾙと画像IDを持つｶﾗﾑ名
        (string Table, string Column)[] tables = [
            (TableOf<FileRecord>(), "id"),
            (TableOf<CaptionRecord>(), "id"),
            (TableOf<ErrorRecord>(), "id"),
            (TableOf<ImageReadRecord>(), "id"),
            (TableOf<MetadataRecord>(), "image_id"),
            (TableOf<TagRecord>(), "image_id"),
        ];
        int rmv = 0;
        int era = 0;
        TransactionOf(transaction => {
            foreach (var t in tables) {
                var sql = $"DELETE FROM {t.Table} WHERE {t.Column}=@id";
                using var command = new SQLiteCommand(sql, transaction.Connection, transaction);
                command.Parameters.Add("@id", System.Data.DbType.Int64);
                // 戻り値はﾌｧｲﾙ本体の削除数のみ数える
                var isImoge = t.Table == TableOf<FileRecord>();
                foreach (var x in files) {
                    if (token.IsCancellationRequested) {
                        return;
                    }
                    command.Parameters["@id"].Value = x.Id;
                    var n = command.ExecuteNonQuery();
                    era += n;
                    if (isImoge) {
                        rmv += n;
                    }
                }
            }
            deleteCount += era;
        }, token);
        ThumbnailStore.Instance.Delete(files, token);
        return rmv;
    }


    /// <summary>
    /// ｷｬﾌﾟｼｮﾝ更新
    /// </summary>
    /// <param name="file"></param>
    /// <param name="caption"></param>
    /// <param name="token"></param>
    /// <exception cref="ArgumentNullException"></exception>
    public void UpdateCaption(FileRecord file, Gachas caption, CancellationToken token) {
        if (!CanWrite) {
            throw new ArgumentNullException(Properties.Resources.SM_E_NotOpenOrReadOnlyEx.Morfat(nameof(IndexStore)));
        }
        var tomlString = TomletMain.TomlStringFrom(caption);
        var sql = $"INSERT INTO captions (id, model, caption) VALUES(@id, @model, @caption) ON CONFLICT(id) DO UPDATE SET model=@model, caption=@caption;";
        TransactionOf(sql, (cmd, transaction) => {
            cmd.Parameters.Add("@id", System.Data.DbType.Int64);
            cmd.Parameters.Add("@model", System.Data.DbType.String);
            cmd.Parameters.Add("@caption", System.Data.DbType.String);
            cmd.Parameters["@id"].Value = file.Id;
            cmd.Parameters["@model"].Value = caption.Model;
            cmd.Parameters["@caption"].Value = tomlString;
            cmd.ExecuteNonQuery();
        }, token);
        if (token.IsCancellationRequested) {
            return;
        }

        file.HasCaption = true;
        file.CaptionModel = caption.Model;
        //file.Lighting = caption.Lighting;
        file.TimeOfDay = caption.TimeOfDay;
        file.DominantColorPalette = caption.DominantColorPalette;
        file.PeopleCount = caption.PeopleCount;
        file.Keywords = string.Join(',', caption.Keywords.Distinct());
        file.Scene = caption.Details.Scene;
        file.Subjects = caption.Details.Subjects;
        file.Action = caption.Details.Action;
        file.Mood = caption.Details.Mood;
        file.ShotType = caption.Details.ShotType;
        file.Costume = string.Join(',', caption.Details.Costume.Where(w => w != "不明" && w != "unclear").Distinct());
        file.Objects = string.Join(',', caption.Details.Objects.Where(w => w != "不明" && w != "unclear").Distinct());
        file.UseCases = string.Join(',', caption.Details.UseCases.Where(w => w != "不明" && w != "unclear").Distinct());

        Update(file, token);
    }

    /// <summary>
    /// ｴﾗーﾚｺーﾄﾞ更新
    /// </summary>
    /// <param name="id"></param>
    /// <param name="type"></param>
    /// <param name="token"></param>
    /// <exception cref="ArgumentNullException"></exception>
    public void SetError(long id, ErrorType type, CancellationToken token)
    {
        if (!CanWrite) {
            throw new ArgumentNullException(Properties.Resources.SM_E_NotOpenOrReadOnlyEx.Morfat(nameof(IndexStore)));
        }
        TransactionOf($"INSERT INTO errors (id, error_type) VALUES(@id, @error_type) ON CONFLICT(id) DO UPDATE SET error_type = @error_type;",
            (cmd, transaction) => {
            cmd.Parameters.Add("@id", System.Data.DbType.Int64);
            cmd.Parameters.Add("@error_type", System.Data.DbType.String);
            cmd.Parameters["@id"].Value = id;
            cmd.Parameters["@error_type"].Value = type.ToString();
            cmd.ExecuteNonQuery();
        }, token);
    }

    /// <summary>
    /// ｴﾗーﾚｺーﾄﾞ削除
    /// </summary>
    /// <param name="id"></param>
    /// <param name="token"></param>
    /// <exception cref="ArgumentNullException"></exception>
    public void RemoveError(long id, CancellationToken token)
    {
        if (!CanWrite) {
            throw new ArgumentNullException(Properties.Resources.SM_E_NotOpenOrReadOnlyEx.Morfat(nameof(IndexStore)));
        }
        TransactionOf($"DELETE FROM errors WHERE id = @id;", (cmd, transaction) => {
            cmd.Parameters.Add("@id", System.Data.DbType.Int64);
            cmd.Parameters["@id"].Value = id;
            deleteCount += cmd.ExecuteNonQuery();
        }, token);
    }

    /// <summary>
    /// ｴﾗーﾚｺーﾄﾞ一括削除
    /// </summary>
    /// <param name="files"></param>
    /// <param name="token"></param>
    /// <exception cref="ArgumentNullException"></exception>
    public void RemoveErrors(IEnumerable<FileRecord> files, CancellationToken token)
    {
        if (!CanWrite) {
            throw new ArgumentNullException(Properties.Resources.SM_E_NotOpenOrReadOnlyEx.Morfat(nameof(IndexStore)));
        }
        TransactionOf($"DELETE FROM errors WHERE id = @id;", (cmd, transaction) => {
            cmd.Parameters.Add("@id", System.Data.DbType.Int64);
            foreach (var x in files) {
                if (token.IsCancellationRequested) {
                    return;
                }
                cmd.Parameters["@id"].Value = x.Id;
                deleteCount += cmd.ExecuteNonQuery();
            }
        }, token);
    }

    /// <summary>
    /// ｴﾗーﾚｺーﾄﾞ全取得
    /// </summary>
    /// <returns></returns>
    public IEnumerable<ErrorRecord> ErrorAll()
    {
        using var justify = JustifyReader();
        using var cmd = new SQLiteCommand(justify.Connection);
        cmd.CommandText = $"SELECT * FROM errors;";
        using var reader = cmd.ExecuteReader();
        while (reader.Read()) {
            yield return MapInstance<ErrorRecord>(reader!);
        }
    }

    /// <summary>
    /// ｶｳﾝﾀー取得
    /// </summary>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    public long GetLastCounting() {
        using var justify = JustifyReader();
        return ReadCounting(justify.Connection, null);
    }

    /// <summary>
    /// ｶｳﾝﾀー取得(書き込みﾄﾗﾝｻﾞｸｼｮﾝ内)
    /// </summary>
    /// <param name="transaction"></param>
    /// <returns></returns>
    long GetLastCounting(SQLiteTransaction transaction) => ReadCounting(transaction.Connection, transaction);

    /// <summary>
    /// ｶｳﾝﾀー読み込み
    /// </summary>
    /// <param name="target"></param>
    /// <param name="transaction"></param>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    static long ReadCounting(SQLiteConnection target, SQLiteTransaction? transaction) {
        using var cmd = new SQLiteCommand(target) { Transaction = transaction };
        cmd.CommandText = $"SELECT count FROM counting;";
        using var reader = cmd.ExecuteReader();
        while (reader.Read()) {
            return reader.GetInt64(0);
        }
        throw new Exception(Properties.Resources.SM_E_CountingFailed);
    }

    /// <summary>
    /// ｶｳﾝﾀー保存
    /// </summary>
    /// <param name="index"></param>
    /// <exception cref="ArgumentNullException"></exception>
    public void StoreCounting(long index) {
        if (!CanWrite) {
            throw new ArgumentNullException(Properties.Resources.SM_E_NotOpenOrReadOnlyEx.Morfat(nameof(IndexStore)));
        }
        TransactionOf($"UPDATE counting SET count=@count;", (cmd, transaction) => {
            cmd.Parameters.Add("@count", System.Data.DbType.Int64);
            cmd.Parameters["@count"].Value = index;
            cmd.ExecuteNonQuery();
        }, CancellationToken.None); // こいつはｷｬﾝｾﾙさせない
    }

    /// <summary>
    /// 実ﾚｺーﾄﾞ数取得
    /// </summary>
    /// <returns></returns>
    public int GetImogeCount() {
        using var justify = JustifyReader();
        using var cmd = new SQLiteCommand(justify.Connection);
        cmd.CommandText = $"SELECT COUNT(id) FROM imoge_index;";
        using var reader = cmd.ExecuteReader();
        while (reader.Read()) {
            return reader!.GetInt32(0);
        }
        return 0;
    }

    /// <summary>
    /// 1ﾚｺーﾄﾞ取得
    /// </summary>
    /// <param name="id"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    public FileRecord? GetFileRecord(long id, CancellationToken token)
    {
        if (!recordTypeMap.TryGetValue(typeof(FileRecord), out var prop)) {
            throw new Exception(Properties.Resources.SM_E_NotInTypeMap.Morfat(nameof(FileRecord)));
        }
        using var justify = JustifyReader();
        using var cmd = new SQLiteCommand(justify.Connection);
        cmd.CommandText = $"SELECT * FROM imoge_index WHERE id = @id;";
        cmd.Parameters.Add("@id", System.Data.DbType.Int64);
        cmd.Parameters["@id"].Value = id;
        using var reader = cmd.ExecuteReader();
        while (reader.Read()) {
            if (token.IsCancellationRequested) {
                break;
            }
            return MapInstance<FileRecord>(reader!, prop);
        }
        return null;
    }

    /// <summary>
    /// ｲﾝﾃﾞｯｸｽ全取得
    /// </summary>
    /// <param name="token"></param>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    public IEnumerable<FileRecord> QueryAll(CancellationToken token) {
        if (!recordTypeMap.TryGetValue(typeof(FileRecord), out var prop)) {
            throw new Exception(Properties.Resources.SM_E_NotInTypeMap.Morfat(nameof(FileRecord)));
        }
        using var justify = JustifyReader();
        using var cmd = new SQLiteCommand(justify.Connection);
        cmd.CommandText = $"SELECT * FROM imoge_index;";
        using var reader = cmd.ExecuteReader();
        while (reader.Read()) {
            if (token.IsCancellationRequested) {
                break;
            }
            yield return MapInstance<FileRecord>(reader!, prop);
        }
    }

    /// <summary>
    /// ｲﾝﾃﾞｯｸｽ全取得
    /// </summary>
    /// <param name="token"></param>
    /// <param name="sortKey"></param>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    public IEnumerable<FileRecord> QueryAll(CancellationToken token, string[] sortKey) {
        if (!recordTypeMap.TryGetValue(typeof(FileRecord), out var prop)) {
            throw new Exception(Properties.Resources.SM_E_NotInTypeMap.Morfat(nameof(FileRecord)));
        }
        // まぁ、ないとは思うけど列ﾁｪｯｸ
        foreach (var cc in sortKey) {
            if (!prop.Schemas.Any(w => w.Name == cc)) {
                throw new Exception(Properties.Resources.SM_E_UnknownColumn.Morfat(cc));
            }
        }

        using var justify = JustifyReader();
        using var cmd = new SQLiteCommand(justify.Connection);
        var sk = string.Join(',', sortKey);
        cmd.CommandText = $"SELECT * FROM imoge_index order by {sk};";
        using var reader = cmd.ExecuteReader();
        while (reader.Read()) {
            if (token.IsCancellationRequested) {
                break;
            }
            yield return MapInstance<FileRecord>(reader!, prop);
        }
    }

    /// <summary>
    /// ﾒﾀﾃﾞーﾀﾑ取得
    /// </summary>
    /// <param name="id"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    public IEnumerable<MetadataRecord> GetMetadata(long id, CancellationToken token) {
        using var justify = JustifyReader();
        using var cmd = new SQLiteCommand(justify.Connection);
        cmd.CommandText = "SELECT * FROM metadata where image_id = @id;";
        cmd.Parameters.Add("@id", System.Data.DbType.Int64);
        cmd.Parameters["@id"].Value = id;
        using var reader = cmd.ExecuteReader();
        while (reader.Read()) {
            if (token.IsCancellationRequested) {
                break;
            }
            yield return MapInstance<MetadataRecord>(reader!);
        }
    }

    /// <summary>
    /// ﾀｸﾞ指定でﾒﾀﾃﾞーﾀﾑ取得
    /// </summary>
    /// <param name="tag"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    public IEnumerable<MetadataRecord> GetMetadataOfTag(string tag, CancellationToken token)
    {
        using var justify = JustifyReader();
        using var cmd = new SQLiteCommand(justify.Connection);
        cmd.CommandText = "SELECT * FROM metadata where tag = @tag;";
        cmd.Parameters.Add("@tag", System.Data.DbType.String);
        cmd.Parameters["@tag"].Value = tag;
        using var reader = cmd.ExecuteReader();
        while (reader.Read()) {
            if (token.IsCancellationRequested) {
                break;
            }
            yield return MapInstance<MetadataRecord>(reader!);
        }
    }

    /// <summary>
    /// ﾒﾀﾃﾞーﾀﾑ削除
    /// </summary>
    /// <param name="files"></param>
    /// <param name="token"></param>
    public void DeleteMetadata(IEnumerable<FileRecord> files, CancellationToken token) {
        if (!CanWrite) {
            return;
        }
        var sql = $"DELETE FROM metadata WHERE image_id=@image_id";
        TransactionOf(sql, (cmd, transaction) => {
            cmd.Parameters.Add("@image_id", System.Data.DbType.Int64);
            foreach (var x in files) {
                if (token.IsCancellationRequested) {
                    return;
                }
                cmd.Parameters["@image_id"].Value = x.Id;
                deleteCount += cmd.ExecuteNonQuery();
            }
        }, token);
    }

    /// <summary>
    /// ｷｬﾌﾟｼｮﾝ一括削除
    /// </summary>
    /// <param name="files"></param>
    /// <param name="token"></param>
    public void DeleteCaptions(IEnumerable<FileRecord> files, CancellationToken token) {
        if (!CanWrite) {
            return;
        }
        var sql = $"DELETE FROM captions WHERE id=@id";
        TransactionOf(sql, (cmd, transaction) => {
            cmd.Parameters.Add("@id", System.Data.DbType.Int64);
            foreach (var x in files) {
                if (token.IsCancellationRequested) {
                    return;
                }
                cmd.Parameters["@id"].Value = x.Id;
                deleteCount += cmd.ExecuteNonQuery();
            }
        }, token);
    }

    /// <summary>
    /// 未加工ｷｬﾌﾟｼｮﾝ取得
    /// </summary>
    /// <param name="id"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    public string? GetCaption(long id, CancellationToken token) {
        using var justify = JustifyReader();
        using var cmd = new SQLiteCommand(justify.Connection);
        cmd.CommandText = "SELECT caption FROM captions where id = @id;";
        cmd.Parameters.Add("@id", System.Data.DbType.Int64);
        cmd.Parameters["@id"].Value = id;
        using var reader = cmd.ExecuteReader();
        while (reader.Read()) {
            if (token.IsCancellationRequested) {
                break;
            }
            return reader?.GetString(0);
        }
        return null;
    }

    /// <summary>
    /// ﾃーﾌﾞﾙ存在ﾁｪｯｸ
    /// </summary>
    /// <param name="transaction"></param>
    /// <param name="tableName"></param>
    /// <returns></returns>
    static bool TableExists(SQLiteTransaction transaction, string tableName) {
        using var command = new SQLiteCommand(
            "SELECT 1 FROM sqlite_master WHERE type='table' AND name=@name LIMIT 1;",
            transaction.Connection, transaction);
        command.Parameters.Add("@name", System.Data.DbType.String).Value = tableName;
        return command.ExecuteScalar() != null;
    }


    /// <summary>
    /// ﾃﾞｨﾚｸﾄﾘ情報取得
    /// </summary>
    /// <returns></returns>
    public IEnumerable<DirectoryIndexRecord> GetDirectoryIndex()
    {
        using var justify = JustifyReader();
        using var cmd = new SQLiteCommand(justify.Connection);
        cmd.CommandText = $"SELECT * FROM directory_index;";
        using var reader = cmd.ExecuteReader();
        while (reader.Read()) {
            yield return MapInstance<DirectoryIndexRecord>(reader!);
        }
    }

    /// <summary>
    /// ﾃﾞｨﾚｸﾄﾘ情報保存
    /// </summary>
    /// <param name="dtm"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    public int SaveDirectoryIndex(IEnumerable<DirectoryIndexRecord> dtm, CancellationToken token)
    {
        if (!CanWrite) {
            return 0;
        }
        int era = 0;
        TransactionOf(transaction => {
            var sql = $"INSERT INTO directory_index (path, last_modified) VALUES(@path, @last_modified) ON CONFLICT(path) DO UPDATE SET last_modified = @last_modified;";
            using var command = new SQLiteCommand(sql, transaction.Connection, transaction);
            command.Parameters.Add("@path", System.Data.DbType.String);
            command.Parameters.Add("@last_modified", System.Data.DbType.Int64);
            foreach (var x in dtm.Where(d => d.IsAlive)) {
                if (token.IsCancellationRequested){
                    continue;
                }
                command.Parameters["@path"].Value = x.Path;
                command.Parameters["@last_modified"].Value = x.LastModified;
                era += command.ExecuteNonQuery();
            }
        }, token);
        return era;
    }

    /// <summary>
    /// ﾃﾞｨﾚｸﾄﾘ情報削除
    /// </summary>
    /// <param name="files"></param>
    /// <param name="token"></param>
    /// <exception cref="ArgumentNullException"></exception>
    /// <returns></returns>
    public int Purge(IEnumerable<DirectoryIndexRecord> files, CancellationToken token)
    {
        if (!CanWrite) {
            throw new ArgumentNullException(Properties.Resources.SM_E_NotOpenOrReadOnlyEx.Morfat(nameof(IndexStore)));
        }
        string[] tables = [TableOf<DirectoryIndexRecord>()];
        int era = 0;
        TransactionOf(transaction => {
            foreach (var t in tables) {
                var sql = $"DELETE FROM {t} WHERE path=@path";
                using var command = new SQLiteCommand(sql, transaction.Connection, transaction);
                command.Parameters.Add("@path", System.Data.DbType.String);
                foreach (var x in files) {
                    if (token.IsCancellationRequested) {
                        return;
                    }
                    command.Parameters["@path"].Value = x.Path;
                    era += command.ExecuteNonQuery();
                }
            }
            deleteCount += era;
        }, token);
        return era;
    }

    /// <summary>
    /// vacuum
    /// </summary>
    /// <param name="force"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    public override Task Vacuum(bool force, CancellationToken token) {
        if ((!force && (deleteCount == 0 || !IsWritable) || token.IsCancellationRequested)) {
            return Task.CompletedTask;
        }
        return RunVacuum(token);
    }

    /// <summary>
    /// ｶｳﾝﾀーの整合を取る(ｶｳﾝﾄ前にｸﾗｯｼｭした時用)
    /// </summary>
    void ConfigureCounter()
    {
        if (!IsWritable) {
            return;
        }
        // 最大IDの確認と補正は同じ書き込みﾄﾗﾝｻﾞｸｼｮﾝでやる
        TransactionOf(transaction => {
            long maxId = 0;
            using (var cmd = new SQLiteCommand(transaction.Connection) { Transaction = transaction }) {
                cmd.CommandText = $"SELECT MAX(id) from {TableOf<FileRecord>()};";
                using var reader = cmd.ExecuteReader();
                while(reader.Read()) {
                    var obz = reader.GetValue(0);
                    if (obz is long) {
                        maxId = (long)obz;
                    }
                    break;
                }
            }
            var last = GetLastCounting(transaction);
            if (last <= maxId) {
                using var update = new SQLiteCommand("UPDATE counting SET count=@count;", transaction.Connection, transaction);
                update.Parameters.AddWithValue("@count", maxId + 1);
                update.ExecuteNonQuery();
            }
        }, CancellationToken.None);
    }

    /// <summary>
    /// 前処理
    /// </summary>
    protected override void Sanitize() {
        Type[] tables = [
            typeof(FileRecord), typeof(CaptionRecord), typeof(TagRecord), typeof(MetadataRecord),
            typeof(ErrorRecord), typeof(DirectoryIndexRecord), typeof(ImageReadRecord)];
        InitializeSchema(databaseVersion, tables, transaction => {
            using var cmd = new SQLiteCommand(transaction.Connection) { Transaction = transaction };
            cmd.CommandText = """
                CREATE TABLE IF NOT EXISTS counting(count INTEGER DEFAULT 0);
                INSERT INTO counting(count) SELECT 0 WHERE NOT EXISTS (SELECT 1 FROM counting);
                """;
            cmd.ExecuteNonQuery();
            cmd.CommandText = $"CREATE INDEX IF NOT EXISTS ipv6_{TableOf<MetadataRecord>()} ON {TableOf<MetadataRecord>()}(image_id);";
            cmd.ExecuteNonQuery();
            cmd.CommandText = $"CREATE INDEX IF NOT EXISTS ipv7_{TableOf<MetadataRecord>()} ON {TableOf<MetadataRecord>()}(tag);";
            cmd.ExecuteNonQuery();
            cmd.CommandText = $"CREATE INDEX IF NOT EXISTS ipv6_{TableOf<TagRecord>()} ON {TableOf<TagRecord>()}(image_id);";
            cmd.ExecuteNonQuery();
            CreateSearchSchema(transaction);
        });
        ConfigureCounter();
    }

    #region IDisposable
    protected override void Dispose(bool disposing) {
        base.Dispose(disposing);
        if (!disposedValue) {
            disposedValue = true;
        }
    }

    #endregion
}
