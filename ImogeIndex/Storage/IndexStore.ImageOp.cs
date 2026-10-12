using System.Data;
using System.Data.SQLite;

namespace ImogeIndex.Storage;

// I/Oｴﾗーﾊﾝﾄﾞﾘﾝｸﾞ関連

public partial class IndexStore
{
    /// <summary>
    /// I/Oｴﾗー履歴取得
    /// </summary>
    /// <returns></returns>
    public IEnumerable<ImageReadRecord> GetImageReadRecords()
    {
        using var justify = JustifyReader();
        using var command = new SQLiteCommand("SELECT * FROM image_read_failures;", justify.Connection);
        using var reader = command.ExecuteReader();
        while (reader.Read()) {
            yield return MapInstance<ImageReadRecord>(reader);
        }
    }

    /// <summary>
    /// I/Oｴﾗーを保存
    /// </summary>
    /// <param name="records"></param>
    /// <param name="token"></param>
    public void SaveImageReadRecords(IEnumerable<ImageReadRecord> records, CancellationToken token)
    {
        TransactionOf(transaction => {
            using var command = new SQLiteCommand("""
                INSERT INTO image_read_failures(id, content_hash, failure_count, last_run)
                VALUES(@id, @hash, @count, @run)
                ON CONFLICT(id) DO UPDATE SET content_hash=@hash, failure_count=@count, last_run=@run;
                """, transaction.Connection, transaction);
            command.Parameters.Add("@id", DbType.Int64);
            command.Parameters.Add("@hash", DbType.String);
            command.Parameters.Add("@count", DbType.Int32);
            command.Parameters.Add("@run", DbType.String);
            foreach (var record in records) {
                token.ThrowIfCancellationRequested();
                if (record.Succeeded) {
                    using var clear = new SQLiteCommand("""
                        DELETE FROM image_read_failures WHERE id=@id;
                        DELETE FROM errors WHERE id=@id AND error_type='File';
                        """, transaction.Connection, transaction);
                    clear.Parameters.AddWithValue("@id", record.Id);
                    clear.ExecuteNonQuery();
                    continue;
                }
                command.Parameters["@id"].Value = record.Id;
                command.Parameters["@hash"].Value = (object?)record.ContentHash ?? DBNull.Value;
                command.Parameters["@count"].Value = record.FailureCount;
                command.Parameters["@run"].Value = (object?)record.LastRun ?? DBNull.Value;
                command.ExecuteNonQuery();
            }
        }, token);
    }
}
