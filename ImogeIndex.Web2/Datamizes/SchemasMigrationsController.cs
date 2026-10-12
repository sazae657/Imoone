using ImogeIndex.Storage;
using System.Data.SQLite;

namespace ImogeIndex.Web2;


/// <summary>
/// Murﾏｲｸﾞﾚーｼｮﾝのﾃｽﾄ
/// </summary>
public class SchemasMigrationsController {
    [MurTable("migration_test")]
    public class LatestsRecords {
        [MurColumn("id", MurColumnFlags.PrimaryKey)]
        public long Id { get; set; }

        [MurColumn("score", 2)]
        public int Score { get; set; }

        [MurColumn("label", 3, MurColumnFlags.NotNull, DefaultSql = "'unknown'")]
        public string Label { get; set; } = "";
    }

    [MurTable("migration_test")]
    public class InvalidsRecords {
        [MurColumn("id", MurColumnFlags.PrimaryKey)]
        public long Id { get; set; }

        [MurColumn("score", 2)]
        public int Score { get; set; }

        [MurColumn("label", 3, MurColumnFlags.NotNull)]
        public string Label { get; set; } = "";
    }

    private sealed class TestsStores : DataStoreBase {
        public void Open(string path, bool writable) => OpenConnection(new FileInfo(path), writable);
        public void Initialize(Type type, long version, bool fail = false) =>
            InitializeSchema(version, [type], transaction => {
                if (fail) {
                    throw new InvalidOperationException("初期化失敗のテスト");
                }
            });
    }

    [MurTable("migration_test")]
    public class UniquesRecords {
        [MurColumn("id", MurColumnFlags.PrimaryKey)]
        public long Id { get; set; }

        [MurColumn("code", 2, MurColumnFlags.Unique)]
        public string? Code { get; set; }
    }

    private sealed class DatumsBases : IDisposable {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"imoge-migration-{Guid.NewGuid():N}.db");

        public object? Execute(string sql) {
            using var connection = new SQLiteConnection(new SQLiteConnectionStringBuilder {
                DataSource = Path, Pooling = false,
            }.ToString());
            connection.Open();
            using var command = new SQLiteCommand(sql, connection);
            return command.ExecuteScalar();
        }

        public void Dispose() {
            foreach (var suffix in new[] { "", "-wal", "-shm" }) {
                File.Delete(Path + suffix);
            }
        }
    }

    [Theory(DisplayName ="マイグレーション後に無事開ける")]
    [InlineData(false)]
    [InlineData(true)]
    public void Oppens(bool existing) {
        using var db = new DatumsBases();
        if (existing) {
            db.Execute("CREATE TABLE migration_test(id INTEGER PRIMARY KEY); INSERT INTO migration_test VALUES(42); PRAGMA user_version = 1;");
        }
        using (var store = new TestsStores()) {
            store.Open(db.Path, true);
            store.Initialize(typeof(LatestsRecords), 3);
            store.Initialize(typeof(LatestsRecords), 3);
        }
        Assert.Equal(3L, db.Execute("PRAGMA user_version;"));
        if (!existing) {
            db.Execute("INSERT INTO migration_test(id) VALUES(42);");
        }
        Assert.Equal(42L, db.Execute("SELECT id FROM migration_test;"));
        Assert.Equal(0L, db.Execute("SELECT score FROM migration_test;"));
        Assert.Equal("unknown", db.Execute("SELECT label FROM migration_test;"));
        using var readOnly = new TestsStores();
        readOnly.Open(db.Path, false);
        readOnly.Initialize(typeof(LatestsRecords), 3);
    }

    [Theory(DisplayName = "不正バージョンでロールバック")]
    [InlineData(false)]
    [InlineData(true)]
    public void RollsBacksOnFails(bool failCallback) {
        using var db = new DatumsBases();
        db.Execute("CREATE TABLE migration_test(id INTEGER PRIMARY KEY); INSERT INTO migration_test VALUES(42); PRAGMA user_version = 1;");
        using (var store = new TestsStores()) {
            store.Open(db.Path, true);
            Assert.ThrowsAny<Exception>(() => store.Initialize(failCallback ? typeof(LatestsRecords) : typeof(InvalidsRecords), 3, failCallback));
        }
        Assert.Equal(1L, db.Execute("PRAGMA user_version;"));
        Assert.Equal(1L, db.Execute("SELECT COUNT(*) FROM pragma_table_info('migration_test');"));
        Assert.Equal(42L, db.Execute("SELECT id FROM migration_test;"));
        using var retry = new TestsStores();
        retry.Open(db.Path, true);
        retry.Initialize(typeof(LatestsRecords), 3);
    }

    [Theory(DisplayName = "おかしなDBはそっ閉じ")]
    [InlineData(1, false)]
    [InlineData(4, false)]
    [InlineData(4, true)]
    [InlineData(0, true)]
    [InlineData(3, true)]
    [InlineData(3, false)]
    public void RejectsBrokensDatabases(int version, bool writable) {
        using var db = new DatumsBases();
        db.Execute($"CREATE TABLE migration_test(id INTEGER PRIMARY KEY); PRAGMA user_version = {version};");
        using (var store = new TestsStores()) {
            store.Open(db.Path, writable);
            Assert.Throws<InvalidOperationException>(() => store.Initialize(typeof(LatestsRecords), 3));
        }
        Assert.Equal((long)version, db.Execute("PRAGMA user_version;"));
        Assert.Equal(1L, db.Execute("SELECT COUNT(*) FROM pragma_table_info('migration_test');"));
    }

    [Fact(DisplayName = "おかしな定義は作る前に弾く")]
    public void RejectsFunnysSchemas() {
        using var db = new DatumsBases();
        using (var store = new TestsStores()) {
            store.Open(db.Path, true);
            Assert.Throws<InvalidOperationException>(() => store.Initialize(typeof(LatestsRecords), 2));
        }
        Assert.Equal(0L, db.Execute("PRAGMA user_version;"));
        Assert.Equal(0L, db.Execute("SELECT COUNT(*) FROM sqlite_master WHERE type = 'table';"));
    }

    [Theory(DisplayName = "主キーとUNIQUE制約は後から追加させない")]
    [InlineData("id INTEGER PRIMARY KEY, extra TEXT", false)]
    [InlineData("id INTEGER PRIMARY KEY, score INTEGER DEFAULT 0", false)]
    [InlineData("id INTEGER PRIMARY KEY", true)]
    public void RejectsExceedsAuthorities(string schema, bool unique) {
        using var db = new DatumsBases();
        db.Execute($"CREATE TABLE migration_test({schema}); PRAGMA user_version = 1;");
        using (var store = new TestsStores()) {
            store.Open(db.Path, true);
            Assert.Throws<InvalidOperationException>(() => store.Initialize(unique ? typeof(UniquesRecords) : typeof(LatestsRecords), 3));
        }
        Assert.Equal(1L, db.Execute("PRAGMA user_version;"));
        Assert.Equal(0L, db.Execute("SELECT COUNT(*) FROM pragma_table_info('migration_test') WHERE name IN ('label', 'code');"));
    }
}
