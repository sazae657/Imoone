using ImogeIndex;
using ImogeIndex.Common;
using System.Data.SQLite;
using R = ImogeMerge.Properties.Resources;

namespace ImogeMerge;

/// <summary>
/// ﾏーｼﾞ結果
/// </summary>
public sealed class DatumMergeResult {
    /// <summary>
    /// Centuryの件数
    /// </summary>
    public long CountC { get; init; }
    /// <summary>
    /// Bongoの件数
    /// </summary>
    public long CountB { get; init; }
    /// <summary>
    /// ﾊﾟｽが重複したので捨てたBongoの件数
    /// </summary>
    public long Skipped { get; init; }
    /// <summary>
    /// ﾏーｼﾞ後の件数
    /// </summary>
    public long Total { get; init; }
    /// <summary>
    /// BのIDに足した値
    /// </summary>
    public long Offset { get; init; }
}

/// <summary>
/// Level.dbとThumbnail.dbのﾏーｼﾞ
/// Cをﾍﾞーｽに、BのﾚｺーﾄﾞをIDをずらして追加する
/// </summary>
public static class DatumMerger {
    // 画像IDを持つﾃーﾌﾞﾙとそのｶﾗﾑ
    static readonly (string Table, string Column)[] ImageTables = [
        ("imoge_index", "id"),
        ("captions", "id"),
        ("errors", "id"),
        ("image_read_failures", "id"),
        ("metadata", "image_id"),
        ("tags", "image_id"),
    ];

    const string ThumbnailTable = "thumbnails";
    const string DirectoryTable = "directory_index";

    // 作り直すので中身を捨てる検索ｲﾝﾃﾞｯｸｽ
    static readonly string[] SearchIndexTables = [
        "search_index_definition", "search_index_value", "search_index_number", "search_index_gram",
    ];

    // 個別に面倒を見てるのでﾏーｼﾞ対象外の警告を出さない
    static readonly HashSet<string> HandledTables = new(
        ImageTables.Select(w => w.Table)
            .Concat(SearchIndexTables)
            .Concat([DirectoryTable, "counting", "search_index_revision"]),
        StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// ﾏーｼﾞ
    /// </summary>
    /// <param name="century"></param>
    /// <param name="bongo"></param>
    /// <param name="output"></param>
    /// <param name="warnings"></param>
    /// <returns></returns>
    /// <exception cref="FileNotFoundException"></exception>
    /// <exception cref="InvalidOperationException"></exception>
    public static DatumMergeResult Merge(DirectoryInfo century, DirectoryInfo bongo, DirectoryInfo output, List<string> warnings) {
        var cendex = Require(century.CombineFile(SystemParams.IndexDatumName));
        var bondex = Require(bongo.CombineFile(SystemParams.IndexDatumName));
        var ceumb = Require(century.CombineFile(SystemParams.ThumbnailDatumName));
        var boumb = Require(bongo.CombineFile(SystemParams.ThumbnailDatumName));
        CheckVersion(cendex, bondex);
        CheckVersion(ceumb, boumb);

        var ondex = output.CombineFile(SystemParams.IndexDatumName);
        var oumb = output.CombineFile(SystemParams.ThumbnailDatumName);
        // WALの中身も含めた状態でCを複製してﾍﾞーｽにする
        VacuumInto(cendex, ondex);
        VacuumInto(ceumb, oumb);

        DatumMergeResult rc;
        using (var connection = Open(ondex, false)) {
            Attach(connection, bondex, "b");
            Attach(connection, oumb, "ot");
            Attach(connection, boumb, "bt");
            using (var transaction = connection.BeginTransaction()) {
                rc = MergeInternal(connection, transaction, warnings);
                transaction.Commit();
            }
            Execute(connection, null, "DETACH DATABASE b; DETACH DATABASE bt;");
            // 検索ｲﾝﾃﾞｯｸｽを捨てた分を詰める
            Execute(connection, null, "VACUUM main; VACUUM ot;");
        }
        SQLiteConnection.ClearAllPools();
        return rc;
    }

    static DatumMergeResult MergeInternal(SQLiteConnection connection, SQLiteTransaction transaction, List<string> warnings) {
        // 1行ごとに検索ｲﾝﾃﾞｯｸｽの世代を進めるﾄﾘｶﾞーは邪魔なので消す
        // (IndexStoreが開く時に作り直す)
        foreach (var name in Strings(connection, transaction,
            "SELECT name FROM main.sqlite_master WHERE type = 'trigger' AND name LIKE 'search_index_%';")) {
            Execute(connection, transaction, $"DROP TRIGGER main.{Quote(name)};");
        }

        var countC = Somke(connection, transaction, "SELECT COUNT(*) FROM main.imoge_index;");
        var countB = Somke(connection, transaction, "SELECT COUNT(*) FROM b.imoge_index;");
        var maxId = Somke(connection, transaction, "SELECT IFNULL(MAX(id), -1) FROM main.imoge_index;");
        var counting = Somke(connection, transaction, "SELECT IFNULL(MAX(count), 0) FROM main.counting;");
        var minB = Somke(connection, transaction, "SELECT IFNULL(MIN(id), 0) FROM b.imoge_index;");
        // Bの最小IDがCの採番済みの次に来るようにずらす
        var offset = Math.Max(maxId + 1, counting) - Math.Min(minB, 0);

        // ﾊﾟｽが重複するBのﾚｺーﾄﾞは捨てる(A優先)
        Execute(connection, transaction, """
            CREATE TEMP TABLE merge_paths(path TEXT PRIMARY KEY COLLATE NOCASE) WITHOUT ROWID;
            INSERT OR IGNORE INTO temp.merge_paths SELECT path FROM main.imoge_index;
            CREATE TEMP TABLE merge_ids(id INTEGER PRIMARY KEY);
            INSERT INTO temp.merge_ids
                SELECT id FROM b.imoge_index WHERE path COLLATE NOCASE NOT IN (SELECT path FROM temp.merge_paths);
            """);
        var kept = Somke(connection, transaction, "SELECT COUNT(*) FROM temp.merge_ids;");

        foreach (var (table, column) in ImageTables) {
            CopyRows(connection, transaction, "b", "main", table, column, offset, warnings);
        }
        CopyRows(connection, transaction, "bt", "ot", ThumbnailTable, "id", offset, warnings);

        // 両方にあるﾃﾞｨﾚｸﾄﾘは古い方のﾀｲﾑｽﾀﾝﾌﾟにして次回必ず走査させる
        if (TableExists(connection, transaction, "b", DirectoryTable)) {
            Execute(connection, transaction, $"""
                INSERT INTO main.{DirectoryTable}(path, last_modified)
                    SELECT path, last_modified FROM b.{DirectoryTable} WHERE true
                    ON CONFLICT(path) DO UPDATE SET last_modified = MIN(last_modified, excluded.last_modified);
                """);
        }

        foreach (var table in SearchIndexTables) {
            if (TableExists(connection, transaction, "main", table)) {
                Execute(connection, transaction, $"DELETE FROM main.{table};");
            }
        }
        if (TableExists(connection, transaction, "main", "search_index_revision")) {
            Execute(connection, transaction, "UPDATE main.search_index_revision SET revision = revision + 1;");
        }

        // 採番ｶｳﾝﾀーは次に振るID
        Execute(connection, transaction, "UPDATE main.counting SET count = (SELECT IFNULL(MAX(id), -1) + 1 FROM main.imoge_index);");

        foreach (var name in Strings(connection, transaction,
            "SELECT name FROM b.sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite_%';")) {
            if (!HandledTables.Contains(name)) {
                warnings.Add(R.PMG_W_UnknownTable.Morfat(name));
            }
        }

        return new DatumMergeResult {
            CountC = countC,
            CountB = countB,
            Skipped = countB - kept,
            Total = Somke(connection, transaction, "SELECT COUNT(*) FROM main.imoge_index;"),
            Offset = offset,
        };
    }

    /// <summary>
    /// 画像IDをずらしてｺﾋﾟー
    /// </summary>
    static void CopyRows(SQLiteConnection connection, SQLiteTransaction transaction,
        string source, string target, string table, string idColumn, long offset, List<string> warnings) {
        if (!TableExists(connection, transaction, source, table)) {
            return;
        }
        if (!TableExists(connection, transaction, target, table)) {
            warnings.Add(R.PMG_W_UnknownTable.Morfat(table));
            return;
        }
        var sc = Columns(connection, transaction, source, table);
        var tc = Columns(connection, transaction, target, table);
        var columns = tc.Where(w => sc.Contains(w, StringComparer.OrdinalIgnoreCase)).ToList();
        if (columns.Count != sc.Count || columns.Count != tc.Count) {
            warnings.Add(R.PMG_W_ColumnMismatch.Morfat(table));
        }
        var insert = string.Join(", ", columns.Select(Quote));
        var select = string.Join(", ", columns.Select(w =>
            w.Equals(idColumn, StringComparison.OrdinalIgnoreCase) ? $"{Quote(w)} + @offset" : Quote(w)));
        var sql = $"""
            INSERT INTO {target}.{Quote(table)} ({insert})
                SELECT {select} FROM {source}.{Quote(table)}
                WHERE {Quote(idColumn)} IN (SELECT id FROM temp.merge_ids);
            """;
        using var cmd = new SQLiteCommand(sql, connection, transaction);
        cmd.Parameters.AddWithValue("@offset", offset);
        cmd.ExecuteNonQuery();
    }

    static FileInfo Require(FileInfo file) {
        if (!file.Exists) {
            throw new FileNotFoundException(R.PMG_E_DatumNotFound.Morfat(file.FullName));
        }
        return file;
    }

    /// <summary>
    /// user_versionが一致してる？
    /// </summary>
    static void CheckVersion(FileInfo a, FileInfo b) {
        var va = UserVersion(a);
        var vb = UserVersion(b);
        if (va != vb) {
            throw new InvalidOperationException(R.PMG_E_VersionMismatch.Morfat(a.Name, va, vb));
        }
    }

    static long UserVersion(FileInfo file) {
        using var connection = Open(file, true);
        return Somke(connection, null, "PRAGMA user_version;");
    }

    /// <summary>
    /// 複製
    /// </summary>
    static void VacuumInto(FileInfo source, FileInfo target) {
        using var connection = Open(source, true);
        Execute(connection, null, $"VACUUM INTO '{target.FullName.Replace("'", "''")}';");
    }

    static void Attach(SQLiteConnection connection, FileInfo file, string schema) {
        using var cmd = new SQLiteCommand($"ATTACH DATABASE @path AS {schema};", connection);
        cmd.Parameters.AddWithValue("@path", file.FullName);
        cmd.ExecuteNonQuery();
    }

    static SQLiteConnection Open(FileInfo file, bool readOnly) {
        var connection = new SQLiteConnection(new SQLiteConnectionStringBuilder {
            DataSource = file.FullName,
            ReadOnly = readOnly,
            FailIfMissing = true,
            BusyTimeout = 5000,
            Pooling = false,
        }.ToString());
        connection.Open();
        return connection;
    }

    static bool TableExists(SQLiteConnection connection, SQLiteTransaction? transaction, string schema, string table) {
        using var cmd = new SQLiteCommand(
            $"SELECT COUNT(*) FROM {schema}.sqlite_master WHERE type = 'table' AND name = @name;", connection, transaction);
        cmd.Parameters.AddWithValue("@name", table);
        return Convert.ToInt64(cmd.ExecuteScalar()) > 0;
    }

    static List<string> Columns(SQLiteConnection connection, SQLiteTransaction? transaction, string schema, string table) {
        return Strings(connection, transaction, $"SELECT name FROM pragma_table_info('{table.Replace("'", "''")}', '{schema}');");
    }

    static List<string> Strings(SQLiteConnection connection, SQLiteTransaction? transaction, string sql) {
        var rc = new List<string>();
        using var cmd = new SQLiteCommand(sql, connection, transaction);
        using var reader = cmd.ExecuteReader();
        while (reader.Read()) {
            rc.Add(reader.GetString(0));
        }
        return rc;
    }

    static long Somke(SQLiteConnection connection, SQLiteTransaction? transaction, string sql) {
        using var cmd = new SQLiteCommand(sql, connection, transaction);
        return Convert.ToInt64(cmd.ExecuteScalar());
    }

    static void Execute(SQLiteConnection connection, SQLiteTransaction? transaction, string sql) {
        using var cmd = new SQLiteCommand(sql, connection, transaction);
        cmd.ExecuteNonQuery();
    }

    static string Quote(string name) => "\"" + name.Replace("\"", "\"\"") + "\"";
}
