using System.Data.SQLite;

namespace ImogeIndex.Storage;

// ﾕーｻﾞー定義ﾗﾍﾞﾙ関連

public partial class IndexStore
{
    /// <summary>
    /// 指定IDと同じ中身(ﾌｧｲﾙﾊｯｼｭ)を持つ画像を全部引く(ﾌﾛﾝﾄｴﾝﾄﾞ用)
    /// </summary>
    /// <param name="ids"></param>
    /// <param name="token"></param>
    /// <returns>ID→ﾌｧｲﾙﾊｯｼｭ(指定IDの物も含む)</returns>
    public Dictionary<long, string> GetSameHashFiles(IEnumerable<long> ids, CancellationToken token)
    {
        var result = new Dictionary<long, string>();
        using var jastaway = JustawayReader();
        foreach (var chunk in ids.Distinct().Chunk(LabelStore.ChunkSize)) {
            token.ThrowIfCancellationRequested();
            var names = chunk.Select((_, i) => $"@p{i}").ToArray();
            using var command = new SQLiteCommand($"""
                SELECT id, file_hash FROM {TableOf<FileRecord>()}
                WHERE file_hash IN (SELECT file_hash FROM {TableOf<FileRecord>()} WHERE id IN ({string.Join(",", names)}));
                """, jastaway.Connection);
            for (var i = 0; i < chunk.Length; i++) {
                command.Parameters.AddWithValue(names[i], chunk[i]);
            }
            using var reader = command.ExecuteReader();
            while (reader.Read()) {
                result[reader.GetInt64(0)] = reader.GetString(1);
            }
        }
        return result;
    }
}
