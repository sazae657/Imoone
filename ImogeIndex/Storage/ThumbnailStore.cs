using ImogeIndex.Common;
using ImogeIndex.Zenerators;
using System.Data.SQLite;


namespace ImogeIndex.Storage;

/// <summary>
/// ｻﾑﾈｽﾄｱ
/// </summary>
public class ThumbnailStore : DataStoreBase
{
    static ThumbnailStore? instance;
    public static ThumbnailStore Instance =>
        instance ?? throw new InvalidOperationException(Properties.Resources.SM_E_DatumNotOpenEx.Morfat(nameof(ThumbnailStore)));

    /// <summary>
    /// 開いてる？
    /// </summary>
    public static bool IsOpened => instance != null;

    private bool disposedValue;

    const string TABLE = "thumbnails";

    long databaseVersion = 1;

    long deleteCount = 0;

    ThumbnailStore() { }

    /// <summary>
    /// 開く
    /// </summary>
    /// <param name="writable"></param>
    public static void Open(bool writable)
    {
        if (instance != null) {
            return;
        }
        var tb = Profile.Active.GetThumbnailDatumPath();
        var store = new ThumbnailStore();
        try {
            store.OpenConnection(tb, writable);
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
    /// とじこ
    /// </summary>
    public static void Close()
    {
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
    /// ｻﾑﾈﾁｪｯｸ
    /// </summary>
    /// <param name="file"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    public Task CheckThumbnail(IEnumerable<FileRecord> file, CancellationToken token) => Task.Run(() =>
    {
        if (!IsConnected) {
            return;
        }

        Keys(out var suv, token);
        int nfc = 0;
        try {
            foreach (var x in file) {
                if (token.IsCancellationRequested) {
                    return;
                }

                if (!suv.Contains(x.Id)) {
                    x.HasThumbnail = false;
                    nfc++;
                }
            }
        }
        catch {
            throw;
        }
        finally {
            suv.Clear();
        }
    }, token);

    /// <summary>
    /// 実ﾚｺーﾄﾞ数取得
    /// </summary>
    /// <returns></returns>
    public int Count() {
        using var justify = JustawayReader();
        using var cmd = new SQLiteCommand(justify.Connection);
        cmd.CommandText = $"SELECT COUNT(id) FROM {TABLE};";
        using var reader = cmd.ExecuteReader();
        while (reader.Read()) {
            return reader!.GetInt32(0);
        }
        return 0;
    }

    /// <summary>
    /// ｷー全部取得
    /// </summary>
    /// <returns></returns>
    public IEnumerable<long> Keys()
    {
        using var justify = JustawayReader();
        using var cmd = new SQLiteCommand(justify.Connection);
        cmd.CommandText = $"SELECT id FROM {TABLE};";
        using var reader = cmd.ExecuteReader();
        while (reader.Read()) {
            yield return reader.GetInt64(0);
        }
    }

    /// <summary>
    /// ｷー全部取得
    /// </summary>
    /// <returns></returns>
    public void Keys(out HashSet<long> keyOut, CancellationToken token) {
        var cn = Count();
        keyOut = new HashSet<long>(cn);

        using var justify = JustawayReader();
        using var cmd = new SQLiteCommand(justify.Connection);
        cmd.CommandText = $"SELECT id FROM {TABLE};";
        using var reader = cmd.ExecuteReader();
        while (reader.Read()) {
            if (token.IsCancellationRequested) {
                return;
            }
            keyOut.Add(reader.GetInt64(0));
        }
    }

    /// <summary>
    /// それ入ってる？
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    public bool Contains(long id) {
        using var justify = JustawayReader();
        using var cmd = new SQLiteCommand(justify.Connection);
        cmd.CommandText = $"SELECT id FROM {TABLE} where id=@id;";
        cmd.Parameters.Add("@id", System.Data.DbType.Int64);
        cmd.Parameters["@id"].Value = id;
        using var reader = cmd.ExecuteReader();
        while (reader.Read()) {
            return true;
        }
        return false;
    }

    /// <summary>
    /// ｻﾑﾈ保存
    /// </summary>
    /// <param name="file"></param>
    /// <param name="token"></param>
    public void Set(IEnumerable<FileRecord> file, CancellationToken token)
    {
        if (!CanWrite) {
            return;
        }

        var sql = $"INSERT INTO {TABLE} (id, thumbnail) VALUES(@id, @thumbnail) ON CONFLICT(id) DO UPDATE SET thumbnail = @thumbnail;";
        TransactionOf(sql, (command, transaction) => {
            command.Parameters.Add("@id", System.Data.DbType.Int64);
            command.Parameters.Add("@thumbnail", System.Data.DbType.Binary);
            foreach (var x in file) {
                if (token.IsCancellationRequested) {
                    return;
                }
                if (x.Thumbnail == null) {
                    continue;
                }
                command.Parameters["@id"].Value = x.Id;
                command.Parameters["@thumbnail"].Value = x.Thumbnail;
                command.ExecuteNonQuery();
            }
        }, token);
    }

    /// <summary>
    /// ｻﾑﾈ削除
    /// </summary>
    /// <param name="file"></param>
    /// <param name="token"></param>
    public void Delete(IEnumerable<FileRecord> file, CancellationToken token) {
        if (!CanWrite) {
            return;
        }
        var sql = $"DELETE FROM {TABLE} WHERE id=@id;";
        TransactionOf(sql, (command, transaction) => {
            command.Parameters.Add("@id", System.Data.DbType.Int64);
            foreach (var x in file) {
                if (token.IsCancellationRequested) {
                    return;
                }
                command.Parameters["@id"].Value = x.Id;
                deleteCount += command.ExecuteNonQuery();
            }
        }, token);
    }

    /// <summary>
    /// ｻﾑﾈをｽﾄﾘーﾑで取得
    /// </summary>
    /// <param name="key"></param>
    /// <returns></returns>
    public Stream? GetStream(long key) {
        if (!IsConnected) {
            return null;
        }
        using var justify = JustawayReader();
        using var cmd = new SQLiteCommand(justify.Connection);
        cmd.CommandText = $"SELECT thumbnail FROM {TABLE} where id=@id;";
        cmd.Parameters.Add("@id", System.Data.DbType.Int64);
        cmd.Parameters["@id"].Value = key;
        using var reader = cmd.ExecuteReader();
        while (reader.Read()) {
            return reader.GetStream(0);
        }
        return null;
    }

    /// <summary>
    /// 前処理
    /// </summary>
    protected override void Sanitize() {
        InitializeSchema(databaseVersion, [typeof(ThumbnailRecord)]);
    }

    [MurTable(TABLE)]
    private sealed class ThumbnailRecord {
        [MurColumn("id", MurColumnFlags.PrimaryKey)]
        public long Id { get; set; }

        [MurColumn("thumbnail")]
        public byte[]? Thumbnail { get; set; }
    }

    /// <summary>
    /// vacuum
    /// </summary>
    /// <param name="force"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    public override Task Vacuum(bool force, CancellationToken token) {
        if ((!force && (deleteCount == 0 || !IsWritable)) || token.IsCancellationRequested) {
            return Task.CompletedTask;
        }
        return RunVacuum(token);
    }

    #region IDisposable
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposedValue) {
            disposedValue = true;
        }
    }

    #endregion
}
