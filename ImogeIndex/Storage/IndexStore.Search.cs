using System.Data;
using System.Data.SQLite;

namespace ImogeIndex.Storage;


// n-gram検索ｲﾝﾃﾞｯｸｽ
// (これ以上ﾃﾞｶくなるようならDictatorに分離する)

public partial class IndexStore
{
    void CreateSearchSchema(SQLiteTransaction transaction)
    {
        if (!IsWritable) {
            return;
        }
        using var command = new SQLiteCommand("""
            CREATE TABLE IF NOT EXISTS search_index_definition (
                field TEXT PRIMARY KEY, gram_size INTEGER NOT NULL, version INTEGER NOT NULL,
                max_image_id INTEGER NOT NULL, configuration_hash INTEGER NOT NULL,
                value_type INTEGER NOT NULL);
            CREATE TABLE IF NOT EXISTS search_index_number (
                field TEXT NOT NULL, value REAL NOT NULL, image_ids BLOB NOT NULL,
                PRIMARY KEY(field, value));
            CREATE TABLE IF NOT EXISTS search_index_value (
                field TEXT NOT NULL, value_id INTEGER NOT NULL, value TEXT NOT NULL,
                image_ids BLOB NOT NULL, PRIMARY KEY(field, value_id));
            CREATE TABLE IF NOT EXISTS search_index_gram (
                field TEXT NOT NULL, gram TEXT NOT NULL, value_ids BLOB NOT NULL,
                PRIMARY KEY(field, gram));
            -- 集計元の世代(トリガーで数える)
            CREATE TABLE IF NOT EXISTS search_index_revision (
                id INTEGER PRIMARY KEY, revision INTEGER NOT NULL);
            INSERT OR IGNORE INTO search_index_revision VALUES(0, 0);

            -- 旧定義のトリガーを置き換えるのでこいつは毎回作り直す
            DROP TRIGGER IF EXISTS search_index_image_insert;
            DROP TRIGGER IF EXISTS search_index_image_delete;
            DROP TRIGGER IF EXISTS search_index_image_update;
            DROP TRIGGER IF EXISTS search_index_metadata_insert;
            DROP TRIGGER IF EXISTS search_index_metadata_delete;
            DROP TRIGGER IF EXISTS search_index_metadata_update;
            CREATE TRIGGER search_index_image_insert AFTER INSERT ON imoge_index
            BEGIN
                UPDATE search_index_definition SET version = 0 WHERE version <> 0;
                UPDATE search_index_revision SET revision = revision + 1;
            END;
            CREATE TRIGGER search_index_image_delete AFTER DELETE ON imoge_index
            BEGIN
                UPDATE search_index_definition SET version = 0 WHERE version <> 0;
                UPDATE search_index_revision SET revision = revision + 1;
            END;
            -- UPDATE OF はSET句に列が含まれるだけでブッパされるので値が変わった時だけに絞る
            CREATE TRIGGER search_index_image_update
            AFTER UPDATE OF id, tags, characters, keywords, costume, objects ON imoge_index
            WHEN OLD.id IS NOT NEW.id OR OLD.tags IS NOT NEW.tags
                OR OLD.characters IS NOT NEW.characters OR OLD.keywords IS NOT NEW.keywords
                OR OLD.costume IS NOT NEW.costume OR OLD.objects IS NOT NEW.objects
            BEGIN
                UPDATE search_index_definition SET version = 0 WHERE version <> 0;
                UPDATE search_index_revision SET revision = revision + 1;
            END;
            CREATE TRIGGER search_index_metadata_insert AFTER INSERT ON metadata
            BEGIN
                UPDATE search_index_definition SET version = 0 WHERE field LIKE 'meta:%' AND version <> 0;
                UPDATE search_index_revision SET revision = revision + 1;
            END;
            CREATE TRIGGER search_index_metadata_delete AFTER DELETE ON metadata
            BEGIN
                UPDATE search_index_definition SET version = 0 WHERE field LIKE 'meta:%' AND version <> 0;
                UPDATE search_index_revision SET revision = revision + 1;
            END;
            CREATE TRIGGER search_index_metadata_update AFTER UPDATE ON metadata
            BEGIN
                UPDATE search_index_definition SET version = 0 WHERE field LIKE 'meta:%' AND version <> 0;
                UPDATE search_index_revision SET revision = revision + 1;
            END;
            """, transaction.Connection, transaction);
        command.ExecuteNonQuery();
    }


    /// <summary>
    /// 全ﾌｨーﾙﾄﾞを置き換える
    /// </summary>
    /// <param name="data"></param>
    /// <param name="token"></param>
    /// <param name="ekbVersion"></param>
    /// <exception cref="InvalidOperationException"></exception>
    public void SaveSearchIndex(SearchIndexDataum data, CancellationToken token, long? ekbVersion = null)
    {
        token.ThrowIfCancellationRequested();
        TransactionOf(transaction => {
            if (ekbVersion.HasValue && ReadSearchRevision(transaction) != ekbVersion.Value) {
                // 集計中に横車がぶっ飛んできた
                throw new InvalidOperationException(Properties.Resources.SM_E_DatumTamperingDuringTabulation);
            }
            using var command = new SQLiteCommand("""
                DELETE FROM search_index_definition;
                DELETE FROM search_index_value;
                DELETE FROM search_index_gram;
                DELETE FROM search_index_number;
                """, transaction.Connection, transaction);
            command.ExecuteNonQuery();
            command.CommandText = "INSERT INTO search_index_definition VALUES(@field, @size, @version, @max, @hash, @type);";
            command.Parameters.Add("@field", DbType.String);
            command.Parameters.Add("@size", DbType.Int32);
            command.Parameters.Add("@version", DbType.Int64);
            command.Parameters.Add("@max", DbType.Int64);
            command.Parameters.Add("@hash", DbType.Int64);
            command.Parameters.Add("@type", DbType.Int32);
            foreach (var definition in data.Definitions) {
                token.ThrowIfCancellationRequested();
                command.Parameters["@field"].Value = definition.Field;
                command.Parameters["@size"].Value = definition.GramSize;
                command.Parameters["@version"].Value = definition.Version;
                command.Parameters["@max"].Value = definition.MaxImageId;
                command.Parameters["@hash"].Value = definition.ConfigurationHash;
                command.Parameters["@type"].Value = (int)definition.ValueType;
                command.ExecuteNonQuery();
            }
            command.Parameters.Clear();
            command.CommandText = "INSERT INTO search_index_value VALUES(@field, @id, @value, @ids);";
            command.Parameters.Add("@field", DbType.String);
            command.Parameters.Add("@id", DbType.Int64);
            command.Parameters.Add("@value", DbType.String);
            command.Parameters.Add("@ids", DbType.Binary);
            foreach (var value in data.Values) {
                token.ThrowIfCancellationRequested();
                command.Parameters["@field"].Value = value.Field;
                command.Parameters["@id"].Value = value.ValueId;
                command.Parameters["@value"].Value = value.Value;
                command.Parameters["@ids"].Value = BlobHelper.ToByteArray(value.ImageIds.ToList());
                command.ExecuteNonQuery();
            }
            command.Parameters.Clear();
            command.CommandText = "INSERT INTO search_index_number VALUES(@field, @value, @ids);";
            command.Parameters.Add("@field", DbType.String);
            command.Parameters.Add("@value", DbType.Double);
            command.Parameters.Add("@ids", DbType.Binary);
            foreach (var value in data.NumericValues) {
                token.ThrowIfCancellationRequested();
                command.Parameters["@field"].Value = value.Field;
                command.Parameters["@value"].Value = value.Value;
                command.Parameters["@ids"].Value = BlobHelper.ToByteArray(value.ImageIds.ToList());
                command.ExecuteNonQuery();
            }
            command.Parameters.Clear();
            command.CommandText = "INSERT INTO search_index_gram VALUES(@field, @gram, @ids);";
            command.Parameters.Add("@field", DbType.String);
            command.Parameters.Add("@gram", DbType.String);
            command.Parameters.Add("@ids", DbType.Binary);
            foreach (var gram in data.Grams) {
                token.ThrowIfCancellationRequested();
                command.Parameters["@field"].Value = gram.Field;
                command.Parameters["@gram"].Value = gram.Gram;
                command.Parameters["@ids"].Value = BlobHelper.ToByteArray(gram.ValueIds.ToList());
                command.ExecuteNonQuery();
            }
            token.ThrowIfCancellationRequested();
        }, token);
        token.ThrowIfCancellationRequested();
    }

    /// <summary>
    /// 検索ﾃﾞーﾀﾑのﾊﾞーｼﾞｮﾝ
    /// </summary>
    /// <returns></returns>
    internal long GetSearchDataVersion() {
        // 読み取りなので書き込み接続(ﾛｯｸ)は使わない
        using var justify = JustifyReader();
        return ReadSearchRevision(justify.Connection, null);
    }

    /// <summary>
    /// ﾘﾋﾞｼﾞｮﾝ読み込み
    /// </summary>
    /// <param name="transaction"></param>
    /// <returns></returns>
    static long ReadSearchRevision(SQLiteTransaction transaction)
        => ReadSearchRevision(transaction.Connection, transaction);

    /// <summary>
    /// ﾘﾋﾞｼﾞｮﾝ読み込み
    /// </summary>
    /// <param name="connection"></param>
    /// <param name="transaction"></param>
    /// <returns></returns>
    static long ReadSearchRevision(SQLiteConnection connection, SQLiteTransaction? transaction) {
        using var command = new SQLiteCommand(
            "SELECT revision FROM search_index_revision WHERE id = 0;", connection, transaction);
        return Convert.ToInt64(command.ExecuteScalar() ?? 0L);
    }


    /// <summary>
    /// n-gramｲﾝﾃﾞｯｸｽ取得
    /// </summary>
    /// <param name="token"></param>
    /// <returns></returns>
    public SearchIndexDataum GetSearchIndex(CancellationToken token)
    {
        var data = new SearchIndexDataum();
        ReadTransactionOf(transaction => {
            if (!TableExists(transaction, "search_index_definition")) {
                return;
            }
            using var command = new SQLiteCommand("SELECT * FROM search_index_definition;", transaction.Connection, transaction);
            using (var reader = command.ExecuteReader()) {
                while (reader.Read()) {
                    token.ThrowIfCancellationRequested();
                    data.Definitions.Add(new SearchIndexDefinition {
                        Field = reader.GetString(0),
                        GramSize = reader.GetInt32(1),
                        Version = reader.GetInt64(2),
                        MaxImageId = reader.GetInt64(3),
                        ConfigurationHash = reader.GetInt64(4),
                        ValueType = (MetadataValueType)reader.GetInt32(5),
                    });
                }
            }
            command.CommandText = "SELECT * FROM search_index_value;";
            using (var reader = command.ExecuteReader()) {
                while (reader.Read()) {
                    token.ThrowIfCancellationRequested();
                    data.Values.Add(new SearchIndexValue {
                        Field = reader.GetString(0),
                        ValueId = reader.GetInt64(1),
                        Value = reader.GetString(2),
                        ImageIds = BlobHelper.ToTypedArray<long>((byte[])reader[3]),
                    });
                }
            }
            command.CommandText = "SELECT * FROM search_index_number;";
            using (var reader = command.ExecuteReader()) {
                while (reader.Read()) {
                    token.ThrowIfCancellationRequested();
                    data.NumericValues.Add(new NumericSearchIndexValue {
                        Field = reader.GetString(0),
                        Value = reader.GetDouble(1),
                        ImageIds = BlobHelper.ToTypedArray<long>((byte[])reader[2]),
                    });
                }
            }
            command.CommandText = "SELECT * FROM search_index_gram;";
            using (var reader = command.ExecuteReader()) {
                while (reader.Read()) {
                    token.ThrowIfCancellationRequested();
                    data.Grams.Add(new SearchIndexGram {
                        Field = reader.GetString(0),
                        Gram = reader.GetString(1),
                        ValueIds = BlobHelper.ToTypedArray<long>((byte[])reader[2]),
                    });
                }
            }
        }, token);
        return data;
    }
}
