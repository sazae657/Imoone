using ImogeIndex.Common;
using System.Data;
using System.Data.SQLite;

namespace ImogeIndex.Storage;

/// <summary>
/// ﾕーｻﾞー定義ﾗﾍﾞﾙ
/// </summary>
[MurTable("labels")]
public class LabelRecord
{
    // 移動やｲﾝﾃﾞｯｸｽ作り直しで消えないようにﾌｧｲﾙﾊｯｼｭで紐付ける
    [MurColumn("file_hash", MurColumnFlags.PrimaryKey | MurColumnFlags.NotNull)]
    public string FileHash { get; set; } = string.Empty;

    [MurColumn("label", MurColumnFlags.NotNull)]
    public string Label { get; set; } = string.Empty;

    /// <summary>
    /// 更新日時(UNIX秒)
    /// </summary>
    [MurColumn("updated_at")]
    public long UpdatedAt { get; set; }
}

/// <summary>
/// ﾗﾍﾞﾙｽﾄｱ
/// これはﾌﾛﾝﾄｴﾝﾄﾞ用なので専用DBに
/// </summary>
public class LabelStore : DataStoreBase
{
    static LabelStore? instance;

    public static LabelStore Instance =>
        instance ?? throw new InvalidOperationException(Properties.Resources.SM_E_DatumNotOpenEx.Morfat(nameof(LabelStore)));

    /// <summary>
    /// 開いてる？
    /// </summary>
    public static bool IsOpened => instance != null;

    const long databaseVersion = 1;

    /// <summary>
    /// IN句に詰めるﾊﾟﾗﾒーﾀー数
    /// </summary>
    internal const int ChunkSize = 500;

    LabelStore() { }

    /// <summary>
    /// 開く(ﾌﾛﾝﾄｴﾝﾄﾞから付け外しするので常に書き込み可)
    /// </summary>
    public static void Open()
    {
        if (instance != null) {
            return;
        }
        var path = Profile.Active.GetLabelDatumPath();
        var store = new LabelStore();
        try {
            store.OpenConnection(path, true);
        }
        catch {
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
    public static void Oppen()
    {
        Close();
        Open();
    }

    /// <summary>
    /// 全ﾗﾍﾞﾙ取得(ﾌｧｲﾙﾊｯｼｭ→ﾗﾍﾞﾙ)
    /// 同じﾗﾍﾞﾙの文字列は共有させる
    /// </summary>
    /// <param name="token"></param>
    /// <returns></returns>
    public Dictionary<string, string> LoadAll(CancellationToken token)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        var pool = new Dictionary<string, string>(StringComparer.Ordinal);
        using var justaway = JustawayReader();
        using var command = new SQLiteCommand($"SELECT file_hash, label FROM {TableOf<LabelRecord>()};", justaway.Connection);
        using var reader = command.ExecuteReader();
        while (reader.Read()) {
            token.ThrowIfCancellationRequested();
            var label = reader.GetString(1);
            if (!pool.TryGetValue(label, out var shared)) {
                pool.Add(label, label);
                shared = label;
            }
            result[reader.GetString(0)] = shared;
        }
        return result;
    }

    /// <summary>
    /// ﾗﾍﾞﾙの付け外し
    /// </summary>
    /// <param name="hashes">対象のﾌｧｲﾙﾊｯｼｭ</param>
    /// <param name="label">nullか空なら外す</param>
    /// <param name="token"></param>
    public void SetLabel(IEnumerable<string> hashes, string? label, CancellationToken token)
    {
        if (!CanWrite) {
            throw new InvalidOperationException(Properties.Resources.SM_E_NotOpenOrReadOnlyEx.Morfat(nameof(LabelStore)));
        }
        var remove = string.IsNullOrEmpty(label);
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        TransactionOf(transaction => {
            using var command = new SQLiteCommand(remove
                ? $"DELETE FROM {TableOf<LabelRecord>()} WHERE file_hash=@hash;"
                : $"""
                    INSERT INTO {TableOf<LabelRecord>()}(file_hash, label, updated_at) VALUES(@hash, @label, @updated)
                    ON CONFLICT(file_hash) DO UPDATE SET label=@label, updated_at=@updated;
                    """, transaction.Connection, transaction);
            command.Parameters.Add("@hash", DbType.String);
            if (!remove) {
                command.Parameters.AddWithValue("@label", label);
                command.Parameters.AddWithValue("@updated", now);
            }
            foreach (var hash in hashes.Distinct(StringComparer.Ordinal)) {
                token.ThrowIfCancellationRequested();
                command.Parameters["@hash"].Value = hash;
                command.ExecuteNonQuery();
            }
        }, token);
    }

    /// <summary>
    /// 前処理
    /// </summary>
    protected override void Sanitize()
    {
        InitializeSchema(databaseVersion, [typeof(LabelRecord)]);
    }
}
