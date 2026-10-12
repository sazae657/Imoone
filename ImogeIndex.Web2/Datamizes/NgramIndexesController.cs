using ImogeIndex.Storage;
using ImogeIndex.Mur;
using System.Data.SQLite;
using System.Reflection;
using System.Text;

namespace ImogeIndex.Web2;


/// <summary>
/// N-gramｲﾝﾃﾞｯｸｽのﾃｽﾄ
/// </summary>
public class NgramIndexesController {

    [Theory(DisplayName ="通常検索")]
    [InlineData("tag", 3)]
    [InlineData("character", 3)]
    [InlineData("kwd", 2)]
    [InlineData("costume", 2)]
    [InlineData("object", 2)]
    [InlineData("meta:model", 3)]
    public void IndexSearch1(string field, int size) {
        var data = Jailing(field,
            (" GOLDEN_BALL ", [1, 2, 2]), ("golden_ball", [2, 3]),
            ("奈良征服", [4]), ("千葉の征服", [5]), ("千葉県", [6]),
            ("abc--bcd", [7]), ("ab--bc", [8]), ("aaaa", [9]),
            ("a", [10]), ("💩埴", [11]), ("ab😀", [12]), ("ab🎉x", [13]), ("😀😀😀", [14]));
        Assert.Equal(size, Assert.Single(data.Definitions).GramSize);
        Assert.Equal([1L, 2L, 3L], data.Values.Single(x => x.Value == "golden_ball").ImageIds);
        // ｻﾛｹﾞーﾄﾍﾟｱを分断したgramを作ってないか(UTF-8往復で化けないこと)
        Assert.All(data.Grams, x => {
            Assert.Equal(x.Gram, Encoding.UTF8.GetString(Encoding.UTF8.GetBytes(x.Gram)));
            Assert.Equal(size, SearchIndexDataum.CodePointLength(x.Gram));
        });
        var index = NgramInvertedIndex.Sanitize(data, 100, 0, TestContext.Current.CancellationToken)[field];
        string[] queries = ["a", "bl", "BLUE", "征服", "県", "千葉", "千葉県", "abcd",
            "abc", "aaaa", "aaaaa", "存在しない", "💩", "💩埴",
            "😀", "b😀", "ab😀", "ab🎉", "🎉x", "😀😀", "😀😀😀😀"];
        foreach (var q in queries) {
            var normalized = SearchIndexDataum.Normalize(q);
            var expected = data.Values.Where(x => x.Value.Contains(normalized, StringComparison.Ordinal))
                .SelectMany(x => x.ImageIds).Distinct().Order();
            var actual = new IdSet();
            index.Lookup(normalized, actual);
            Assert.Equal(expected, actual.Order());
        }
    }

    [Fact(DisplayName = "順序バラバラでもちゃんとID解決しろ")]
    public void IndexSearch2() {
        var data = Jailing("kwd", ("奈良征服", [10]), ("長野征服", [20]));
        foreach (var x in data.Values) {
            x.ValueId = 1000 - x.ValueId * 10;
        }
        foreach (var x in data.Grams) {
            x.ValueIds = x.ValueIds.Select(x => 1000 - x * 10).Order().ToArray();
        }
        data.Values.Reverse();
        var index = NgramInvertedIndex.Sanitize(data, 100, 0, TestContext.Current.CancellationToken)["kwd"];
        var ids = new IdSet();
        index.Lookup("奈良", ids);
        Assert.Equal([10L], ids);
    }

    [Theory(DisplayName = "インデックス効くやつと効かんやつを混合")]
    [InlineData("tag", "golden_ball")]
    [InlineData("character", "yuzuko")]
    [InlineData("kwd", "征服")]
    [InlineData("costume", "征服")]
    [InlineData("object", "外科")]
    public void IndexSearch3(string field, string value) {
        var data = Jailing(field, (value, [1, 3]), ("other", [2]));
        var indexed = new ImogeDatum();
        LoadIndices(indexed, data);
        var fallback = new ImogeDatum();
        var items = new[] {
            Item(1, value + ", other", 2024), Item(2, "other", 2025), Item(3, value, 2026),
        };
        string[] queries = [
            $"{field}:{value}", $"{field}:{value[..1]}", $"{field}:missing",
            $"{field}:{value} | dc:2025", $"{field}:missing | dc:2025",
            $"{field}:{value} dc:2024", $"{field}:{value} | {field}:other",
            $"{field}:\"{value}, other\"", $"{field}:\" other\"",
        ];
        foreach (var q in queries) {
            var query = new MurQuery();
            query.Parse(q);
            query.PrepareTag(fallback, CancellationToken.None);
            var expected = items.Where(x => query.Evaluate(x, CancellationToken.None)).Select(x => x.Id).ToArray();
            query.PrepareTag(indexed, CancellationToken.None);
            Assert.Equal(expected, items.Where(x => query.Evaluate(x, CancellationToken.None)).Select(x => x.Id));
        }
    }

    [Fact(DisplayName = "再読み込みしても検索中に取った世代は変わらない？")]
    public void CaptureKeepsGenerationAcrossReload() {
        var datum = new ImogeDatum();
        LoadIndices(datum, Jailing("tag", ("golden_ball", [1])));
        var snapshot = datum.Capture();
        Assert.Same(snapshot.Version, datum.Version);

        LoadIndices(datum, Jailing("tag", ("silver_ball", [2])));
        Assert.NotSame(snapshot.Version, datum.Version);

        Assert.True(snapshot.TryGetIndex(SearchField.Tag, out var old));
        var ids = new IdSet();
        old.Lookup("golden", ids);
        Assert.Equal([1L], ids);
        Assert.True(datum.TryGetIndex(SearchField.Tag, out var reloaded));
        ids.Clear();
        reloaded.Lookup("golden", ids);
        Assert.Empty(ids);
    }

    [Theory(DisplayName = "サロゲートペアを含んでもDB往復で壊れない？")]
    [InlineData("tag")]
    [InlineData("kwd")]
    public void IndexSearch4(string field) {
        using var dtm = new TestIndexDatum();
        // 旧実装だと"ab\uD83D"と"ab\uD83C"がどちらも"ab�"に化けて主ｷー衝突した
        var data = Jailing(field, ("ab😀", [1]), ("ab🎉", [2]), ("😀", [3]),
            ("x\uD83Dy", [4]), ("x\uDE00y", [5]));
        dtm.Store.SaveSearchIndex(data, TestContext.Current.CancellationToken);
        var loaded = dtm.Store.GetSearchIndex(TestContext.Current.CancellationToken);
        var index = NgramInvertedIndex.Sanitize(loaded, 100, 0, TestContext.Current.CancellationToken)[field];
        (string Query, long[] Expected)[] cases = [
            ("😀", [1, 3]), ("b😀", [1]), ("ab😀", [1]), ("ab🎉", [2]), ("ab", [1, 2]),
            // 片割れは正規化でU+FFFDに寄るので、どちらの片割れでも同じものに当たる
            ("x\uD83Dy", [4, 5]), ("x�", [4, 5]),
        ];
        foreach (var (query, expected) in cases) {
            var ids = new IdSet();
            index.Lookup(FileItem.Normalize(query), ids);
            Assert.Equal(expected, ids.Order());
        }
    }

    //
    // ここからﾃﾞーﾀﾑI/O
    //

    [Fact(DisplayName = "非互換データでは空結果を返せ")]
    public void IndexDatum1()
    {
        var data = Jailing("tag", ("golden_ball", [1]));
        Assert.Empty(NgramInvertedIndex.Sanitize(data, 101, 0, TestContext.Current.CancellationToken));
        data.Definitions[0].GramSize = 2;
        Assert.Empty(NgramInvertedIndex.Sanitize(data, 100, 0, TestContext.Current.CancellationToken));
        data.Definitions[0].GramSize = 3;
        data.Definitions[0].Version++;
        Assert.Empty(NgramInvertedIndex.Sanitize(data, 100, 0, TestContext.Current.CancellationToken));
        data = Jailing("meta:model", ("canon", [1]));
        Assert.Empty(NgramInvertedIndex.Sanitize(data, 100, 1, TestContext.Current.CancellationToken));
    }


    [Fact(DisplayName = "I/O失敗でちゃんとロールバックできてる？")]
    public void IndexDatum2() {
        using var dtm = new TestIndexDatum();
        var original = Jailing("kwd", ("奈良征服", [10]));
        dtm.Store.SaveSearchIndex(original, CancellationToken.None, dtm.Store.GetSearchDataVersion());
        var replacement = Jailing("tag", ("golden_ball", [20]));
        replacement.Values.Add(replacement.Values[0]);
        Assert.Throws<SQLiteException>(() => dtm.Store.SaveSearchIndex(replacement, CancellationToken.None));
        AssertOriginal(dtm.Store.GetSearchIndex(CancellationToken.None));
        replacement.Values.RemoveAt(1);
        dtm.Store.SaveSearchIndex(replacement, CancellationToken.None);
        var loaded = dtm.Store.GetSearchIndex(CancellationToken.None);
        Assert.Equal("tag", Assert.Single(loaded.Definitions).Field);
        Assert.Equal("golden_ball", Assert.Single(loaded.Values).Value);
        Assert.Equal([20L], loaded.Values[0].ImageIds);
    }

    [Fact(DisplayName = "インデックスの保存をキャンセルしてロールバックできる？")]
    public void IndexDatum3() {
        using var dtm = new TestIndexDatum();
        dtm.Store.SaveSearchIndex(Jailing("kwd", ("奈良征服", [10])), CancellationToken.None);
        using var cancellation = new CancellationTokenSource();
        dtm.Connection.Update += (_, args) => {
            if (args.Table == "search_index_value") {
                cancellation.Cancel();
            }
        };
        Assert.Throws<OperationCanceledException>(() =>
            dtm.Store.SaveSearchIndex(Jailing("tag", ("golden_ball", [20])), cancellation.Token));
        AssertOriginal(dtm.Store.GetSearchIndex(CancellationToken.None));
    }

    [Fact(DisplayName = "メタデータムの部分更新ちゃんと出来てる？")]
    public void IndexDatum4() {
        using var dtm = new TestIndexDatum();
        var data = Jailing("tag", ("golden_ball", [10]));
        data.AddField("meta:model", new Dictionary<string, HashSet<long>> { ["canon"] = [10] },
            100, 0, TestContext.Current.CancellationToken);
        dtm.Store.SaveSearchIndex(data, TestContext.Current.CancellationToken);
        using var command = new SQLiteCommand("""
            INSERT INTO metadata(image_id, type, directory, tag, value) VALUES(10, 'test', 'test', 'model', 'nikon');
            """, dtm.Connection);
        command.ExecuteNonQuery();
        var loaded = dtm.Store.GetSearchIndex(TestContext.Current.CancellationToken);
        var indices = NgramInvertedIndex.Sanitize(loaded, 100, 0, TestContext.Current.CancellationToken);
        Assert.True(indices.ContainsKey("tag"));
        Assert.False(indices.ContainsKey("meta:model"));
    }

    [Fact(DisplayName = "内容だけの変更検知できてる？")]
    public void IndexDatum5() {
        using var dtm = new TestIndexDatum();
        using var command = new SQLiteCommand("""
            INSERT INTO imoge_index(id, path, container, file_hash, registered_at, created_at,
                modified_at, file_size, width, height, tags)
            VALUES(10, 'test.png', '', '', 0, 0, 0, 0, 1, 1, 'golden_ball');
            """, dtm.Connection);
        command.ExecuteNonQuery();
        dtm.Store.SaveSearchIndex(Jailing("tag", ("golden_ball", [10])), TestContext.Current.CancellationToken);
        command.CommandText = "UPDATE imoge_index SET tags='red_hair' WHERE id=10;";
        command.ExecuteNonQuery();
        Assert.Empty(NgramInvertedIndex.Sanitize(dtm.Store.GetSearchIndex(TestContext.Current.CancellationToken),
            10, 0, TestContext.Current.CancellationToken));
    }

    [Fact(DisplayName = "他プロセスからの妨害をリジェクト出来てる？")]
    public void IndexDatum6() {
        using var dtm = new TestIndexDatum();
        var version = dtm.Store.GetSearchDataVersion();
        using var other = new SQLiteConnection(dtm.Connection.ConnectionString);
        other.Open();
        using var command = new SQLiteCommand("INSERT INTO metadata(image_id, tag, value) VALUES(10, 'model', 'nikon');", other);
        command.ExecuteNonQuery();
        Assert.Throws<InvalidOperationException>(() => dtm.Store.SaveSearchIndex(
            Jailing("tag", ("golden_ball", [10])), TestContext.Current.CancellationToken, version));
        Assert.Empty(dtm.Store.GetSearchIndex(TestContext.Current.CancellationToken).Definitions);
    }

    [Fact(DisplayName = "同じ接続からの書き込みもリジェクト出来てる？")]
    public void IndexDatum7() {
        using var dtm = new TestIndexDatum();
        var version = dtm.Store.GetSearchDataVersion();
        using var command = new SQLiteCommand("INSERT INTO metadata(image_id, tag, value) VALUES(10, 'model', 'nikon');", dtm.Connection);
        command.ExecuteNonQuery();
        Assert.NotEqual(version, dtm.Store.GetSearchDataVersion());
        Assert.Throws<InvalidOperationException>(() => dtm.Store.SaveSearchIndex(
            Jailing("tag", ("golden_ball", [10])), TestContext.Current.CancellationToken, version));
        Assert.Empty(dtm.Store.GetSearchIndex(TestContext.Current.CancellationToken).Definitions);
    }

    [Fact(DisplayName = "検索対象の値が変わらない更新では無効化しない？")]
    public void IndexDatum8() {
        using var dtm = new TestIndexDatum();
        using var command = new SQLiteCommand("""
            INSERT INTO imoge_index(id, path, container, file_hash, registered_at, created_at,
                modified_at, file_size, width, height, tags)
            VALUES(10, 'test.png', '', '', 0, 0, 0, 0, 1, 1, 'golden_ball');
            """, dtm.Connection);
        command.ExecuteNonQuery();
        dtm.Store.SaveSearchIndex(Jailing("tag", ("golden_ball", [10])), TestContext.Current.CancellationToken);
        var version = dtm.Store.GetSearchDataVersion();
        // 全列UPDATEと同じく検索対象の列もSETに含める
        command.CommandText = "UPDATE imoge_index SET tags=tags, characters=characters, width=2 WHERE id=10;";
        command.ExecuteNonQuery();
        Assert.Equal(version, dtm.Store.GetSearchDataVersion());
        Assert.True(NgramInvertedIndex.Sanitize(dtm.Store.GetSearchIndex(TestContext.Current.CancellationToken),
            10, 0, TestContext.Current.CancellationToken).ContainsKey("tag"));
    }

    /// <summary>
    /// ｲﾝﾃﾞｯｸｽ同一判定
    /// </summary>
    /// <param name="data"></param>
    static void AssertOriginal(SearchIndexDataum data)
    {
        Assert.Equal("kwd", Assert.Single(data.Definitions).Field);
        var ids = new IdSet();
        NgramInvertedIndex.Sanitize(data, 100, 0, TestContext.Current.CancellationToken)["kwd"].Lookup("征服", ids);
        Assert.Equal([10L], ids);
    }

    /// <summary>
    /// ﾀﾞﾐーのﾌｧｲﾙﾚｺーﾄﾞ
    /// </summary>
    /// <param name="id"></param>
    /// <param name="value"></param>
    /// <param name="year"></param>
    /// <returns></returns>
    static FileItem Item(long id, string value, int year) => new() {
        Id = id,
        Tag = value,
        Character = value,
        Keyword = value,
        Costume = value,
        Objects = value,
        DateTime = new DateTime(year, 1, 1),
    };

    /// <summary>
    /// ｲﾝﾃﾞｯｸｽの構築
    /// </summary>
    /// <param name="field"></param>
    /// <param name="values"></param>
    /// <returns></returns>
    internal static SearchIndexDataum Jailing(string field, params (string Value, long[] Ids)[] values)
    {
        var data = new SearchIndexDataum();
        data.AddField(field, values.Select(x =>
            new KeyValuePair<string, HashSet<long>>(x.Value, x.Ids.ToHashSet())),
            100, 0, TestContext.Current.CancellationToken);
        return data;
    }

    /// <summary>
    /// ｲﾝﾃﾞｸｽ読み込み
    /// </summary>
    /// <param name="datum"></param>
    /// <param name="data"></param>
    static void LoadIndices(ImogeDatum datum, SearchIndexDataum data)
    {
        datum.LoadSearchIndices(data, 100L, 0L, TestContext.Current.CancellationToken);
    }
}


/// <summary>
/// 使い捨てﾃﾞーﾀﾑ
/// </summary>
internal sealed class TestIndexDatum : IDisposable {
    readonly string path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid() + ".sqlite");
    public IndexStore Store { get; }
    public SQLiteConnection Connection { get; }

    public TestIndexDatum() {
        Store = (IndexStore)Activator.CreateInstance(typeof(IndexStore), nonPublic: true)!;
        typeof(DataStoreBase).GetMethod("OpenConnection", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(Store, [new FileInfo(path), true]);
        Connection = (SQLiteConnection)typeof(DataStoreBase)
            .GetField("writer", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(Store)!;
    }

    public void Dispose() {
        Store.Dispose();
        File.Delete(path);
        File.Delete(path + "-wal");
        File.Delete(path + "-shm");
    }
}
