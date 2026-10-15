using ImogeIndex.Common;
using ImogeIndex.Imoging;
using ImogeIndex.Storage;
using ImogeIndex.Zenerators;
using ImogeIndex.Mur;
using MetadataExtractor;
using System.Data.SQLite;
using System.Globalization;
using Tomlet;

namespace ImogeIndex.Web2.Mur;

/// <summary>
/// 数値ﾒﾀﾃﾞーﾀﾑ
/// </summary>
public class NumericMetadataController {
    static readonly MetaAlias[] Aliases = [
        new() { Alias = "iso", Tag = "ISO Speed Ratings", ValueType = MetadataValueType.Number },
        new() { Alias = "exposure", Tag = "Exposure Time", ValueType = MetadataValueType.Number },
        new() { Alias = "model", Tag = "Model" },
    ];

    [Theory(DisplayName ="数値クエリーの境界値")]
    [InlineData("100", 100, true)]
    [InlineData("100", 1000, false)]
    [InlineData(">100", 100, false)]
    [InlineData(">100", 101, true)]
    [InlineData(">=100", 100, true)]
    [InlineData("<100", 100, false)]
    [InlineData("<100", 99, true)]
    [InlineData("<=100", 100, true)]
    [InlineData("100-800", 100, true)]
    [InlineData("100-800", 800, true)]
    [InlineData("100-800", 801, false)]
    [InlineData("-2.5--0.5", -2.5, true)]
    [InlineData("-2.5--0.5", -0.5, true)]
    [InlineData("-2.5--0.5", 0, false)]
    [InlineData("0.001-0.008", 0.008, true)]
    [InlineData("1/125", 0.008, true)]
    [InlineData("1/1000-1/125", 0.008, true)]
    [InlineData("<=1/125", 0.01, false)]
    [InlineData("1e-3-8e-3", 0.008, true)]
    public void NumericQuery1(string condition, double number, bool expected) {
        var data = new SearchIndexDataum();
        data.AddNumericField("meta:iso", new Dictionary<double, HashSet<long>> { [number] = [1] }, 10, 0, TestContext.Current.CancellationToken);
        var datum = Load(data);
        var query = Query("meta:iso:" + condition, datum);
        Assert.Equal(expected, query.Evaluate(new FileItem { Id = 1 }, TestContext.Current.CancellationToken));
        Assert.False(query.Evaluate(new FileItem { Id = 2 }, TestContext.Current.CancellationToken));
    }

    [Theory(DisplayName = "不正値")]
    [InlineData("")]
    [InlineData(">")]
    [InlineData("abc")]
    [InlineData("800-100")]
    [InlineData("1-2-3")]
    [InlineData("1/0")]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    [InlineData("1e999")]
    [InlineData("0,008")]
    [InlineData("100 sec")]
    public void NumericQuery2(string condition) {
        var datum = Load(JailedDatum());
        var query = Query("meta:iso:\"" + condition + "\"", datum);
        Assert.False(query.IsEmpty());
        Assert.False(query.Evaluate(new FileItem { Id = 1 }, TestContext.Current.CancellationToken));
        Assert.False(Query("meta:iso:", datum).IsEmpty());
        Assert.False(Query("meta:iso:", datum).Evaluate(new FileItem { Id = 1 }, TestContext.Current.CancellationToken));
    }

    [Fact(DisplayName = "組み合わせ条件マッチしろ")]
    public void NumericQuery3() {
        var datum = Load(JailedDatum());
        Assert.Equal(new long[] { 2 }, Matches("meta:iso:>=400 meta:model:canon", datum));
        Assert.Equal(new long[] { 1, 2, 3 }, Matches("meta:iso:>=400 | meta:model:canon", datum));
        Assert.Equal(new long[] { 2, 4 }, Matches("meta:iso:400 | width:>1000", datum));
        Assert.Equal(new long[] { 2 }, Matches("meta:iso:> | meta:iso:400", datum));
        Assert.Equal(new long[] { 1 }, Matches("meta:exposure:1/125", datum));
        Assert.Equal(new long[] { 2 }, Matches("META:ISO:400", datum));
        Assert.Empty(Matches("meta:iso:0", datum));
        Assert.Empty(Matches("meta:iso:400", new ImogeDatum()));
    }

    [Theory(DisplayName = "短絡評価機能してる？")]
    [InlineData("100", "1000", false)]
    [InlineData(">=100", ">=400", true)]
    [InlineData(">100", ">=100", false)]
    [InlineData("<=800", "100-800", true)]
    [InlineData("100-800", "100-801", false)]
    [InlineData("1/125", "0.008", true)]
    [InlineData(">", ">100", false)]
    public void NumericQuery4(string previous, string current, bool expected) {
        var before = new MurQuery(Aliases);
        var after = new MurQuery(Aliases);
        before.Parse("meta:iso:" + previous);
        after.Parse("meta:iso:" + current);
        Assert.Equal(expected, after.IsNarrowerThan(before));
    }

    [Fact(DisplayName = "エイリアス設定が変わったらパーサーのキャッシュも作り直す")]
    public void ParserCache1() {
        // 数値扱いなら範囲で包含判定、ﾃｷｽﾄ扱いなら部分文字列なので結果が変わる
        static bool Narrower(MetaAlias[] aliases) {
            var before = new MurQuery(aliases);
            var after = new MurQuery(aliases);
            before.Parse("meta:iso:>=100");
            after.Parse("meta:iso:>=400");
            return after.IsNarrowerThan(before);
        }
        MetaAlias[] number = [new() { Alias = "iso", Tag = "ISO Speed Ratings", ValueType = MetadataValueType.Number }];
        MetaAlias[] text = [new() { Alias = "iso", Tag = "ISO Speed Ratings", ValueType = MetadataValueType.Text }];

        Assert.True(Narrower(number));
        Assert.False(Narrower(text));
        Assert.True(Narrower(number));

        // 同じ配列を中身だけ書き換えても拾う
        number[0].ValueType = MetadataValueType.Text;
        Assert.False(Narrower(number));
    }

    [Fact(DisplayName = "Exifから数値にちゃんと戻せる？")]
    public void NumericParse1() {
        Assert.Equal(400d, ImageMetadataAnalyzer.AsNumericValue((ushort)400));
        Assert.Equal(-0.5, ImageMetadataAnalyzer.AsNumericValue(-0.5f));
        Assert.Equal(2.8, ImageMetadataAnalyzer.AsNumericValue(2.8m));
        Assert.Equal(0.008, ImageMetadataAnalyzer.AsNumericValue(new Rational(1, 125)));
        Assert.Equal(0d, ImageMetadataAnalyzer.AsNumericValue(0));
        foreach (var value in new object[] { new Rational(1, 0), double.NaN, double.PositiveInfinity,
            "400", "1/125 sec", new[] { 100, 200 } }) {
            Assert.Null(ImageMetadataAnalyzer.AsNumericValue(value));
        }
    }

    [Fact(DisplayName = "設定読み込み")]
    public void NumericParse2() {
        foreach (var alias in Aliases) {
            Assert.Equal(alias, MetaAlias.FromSettingure(alias.ToSettingure()));
        }
        var settings = new Settings { MetadataSearchTags = Aliases.ToList() };
        var restored = TomletMain.To<Settings>(TomletMain.TomlStringFrom(settings));
        Assert.Equal(Aliases, restored.MetadataSearchTags);
        var text = new MetaAlias { Alias = "ISO", Tag = "ISO Speed Ratings" };
        Assert.Throws<ArgumentException>(() => MetaAlias.ValidateType([Aliases[0], text]));
        Assert.NotEqual(MetadataIndexMarker.CalculateTagHash([Aliases[0]]), MetadataIndexMarker.CalculateTagHash([text]));
        Assert.Throws<ArgumentException>(() => MetaAlias.FromSettingure("iso:integer=ISO"));
    }

    [Fact(DisplayName = "小数点の標準はピリオドだぞ")]
    public void NumericParse3() {
        var previous = CultureInfo.CurrentCulture;
        try {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
            Assert.True(NumericRange.TryParse("0.008", false, out var range));
            Assert.True(range.Contains(0.008));
            Assert.False(NumericRange.TryParse("0,008", false, out _));
        }
        finally {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact(DisplayName = "データム更新")]
    public void Index1() {
        using var db = new TestIndexDatum();
        using var command = new SQLiteCommand("""
            INSERT INTO metadata(image_id, tag, value, numeric_value) VALUES
                (1, 'Exposure Time', '1/125', 0.008), (2, 'Exposure Time', '不明', NULL),
                (3, 'Exposure Time', '0', 0);
            """, db.Connection);
        command.ExecuteNonQuery();
        var records = db.Store.GetMetadataOfTag("Exposure Time", TestContext.Current.CancellationToken).OrderBy(x => x.ImageId).ToArray();
        Assert.Equal(0.008, records[0].NumericValue);
        Assert.Null(records[1].NumericValue);
        Assert.Equal(0d, records[2].NumericValue);
        Assert.Equal(0.008, new ImageMetadataValue(records[0]).NumericValue);
        var collected = TagIndexProzessor.CollectNumericMetadataValues([Aliases[1]], _ => records, TestContext.Current.CancellationToken);
        Assert.Equal(new long[] { 1 }, collected[0.008]);
        Assert.Equal(new long[] { 3 }, collected[0]);
        var data = JailedDatum();
        db.Store.SaveSearchIndex(data, TestContext.Current.CancellationToken);
        var loaded = db.Store.GetSearchIndex(TestContext.Current.CancellationToken);
        Assert.Equal(data.NumericValues.Count, loaded.NumericValues.Count);
        Assert.Equal(new long[] { 1 }, Matches("meta:exposure:0.008", Load(loaded)));
        Assert.False(NgramInvertedIndex.Sanitize(loaded, 10, 0, TestContext.Current.CancellationToken).ContainsKey("meta:iso"));
        Assert.Empty(NumericMetadataIndex.Sanitize(loaded, 11, 0, TestContext.Current.CancellationToken));
        Assert.Empty(NumericMetadataIndex.Sanitize(loaded, 10, 1, TestContext.Current.CancellationToken));
        command.CommandText = "UPDATE metadata SET numeric_value = 0.01 WHERE image_id = 1";
        command.ExecuteNonQuery();
        Assert.Empty(NumericMetadataIndex.Sanitize(db.Store.GetSearchIndex(TestContext.Current.CancellationToken), 10, 0, TestContext.Current.CancellationToken));
    }

    [Fact(DisplayName = "キャンセルからのロールバックできる？")]
    public void Index2() {
        using var db = new TestIndexDatum();
        var data = new SearchIndexDataum();
        data.AddNumericField("meta:iso", new[] {
            new KeyValuePair<double, HashSet<long>>(100, [1, 2]),
            new KeyValuePair<double, HashSet<long>>(100, [2, 3]),
            new KeyValuePair<double, HashSet<long>>(double.NaN, [4]),
        }, 10, 0, TestContext.Current.CancellationToken);
        Assert.Equal(new long[] { 1, 2, 3 }, Assert.Single(data.NumericValues).ImageIds);
        db.Store.SaveSearchIndex(data, TestContext.Current.CancellationToken);
        data.NumericValues.Add(data.NumericValues[0]);
        Assert.Throws<SQLiteException>(() => db.Store.SaveSearchIndex(data, TestContext.Current.CancellationToken));
        Assert.Single(db.Store.GetSearchIndex(TestContext.Current.CancellationToken).NumericValues);
        data.NumericValues.RemoveAt(1);
        using var cancellation = new CancellationTokenSource();
        db.Connection.Update += (_, args) => {
            if (args.Table == "search_index_number") {
                cancellation.Cancel();
            }
        };
        Assert.Throws<OperationCanceledException>(() => db.Store.SaveSearchIndex(data, cancellation.Token));
        Assert.Single(db.Store.GetSearchIndex(TestContext.Current.CancellationToken).NumericValues);
    }

    [Fact(DisplayName = "重複解決")]
    public void Index3() {
        var merged = TagIndexProzessor.CollectNumericMetadataValues([
            new() { Alias = "iso", Tag = "A", ValueType = MetadataValueType.Number },
            new() { Alias = "iso", Tag = "B", ValueType = MetadataValueType.Number },
        ], tag => tag == "A" ? [new() { ImageId = 1, NumericValue = 100 }]
            : [new() { ImageId = 1, NumericValue = 800 }, new() { ImageId = 2 }], TestContext.Current.CancellationToken);
        var data = new SearchIndexDataum();
        data.AddNumericField("meta:iso", merged, 10, 0, TestContext.Current.CancellationToken);
        Assert.Equal(new long[] { 1 }, Matches("meta:iso:>=400", Load(data)));
        Assert.Equal(new long[] { 1 }, Matches("meta:iso:100", Load(data)));
        var empty = new SearchIndexDataum();
        empty.AddNumericField("meta:iso", [], 10, 0, TestContext.Current.CancellationToken);
        Assert.Empty(Matches("meta:iso:>=0", Load(empty)));
    }

    [Fact(DisplayName = "とりま10万件でどんなもんになるベンチ")]
    public void Index4() {
        var token = TestContext.Current.CancellationToken;
        var data = new SearchIndexDataum();
        data.AddNumericField("meta:iso", Enumerable.Range(0, 100000).Select(i =>
            new KeyValuePair<double, HashSet<long>>(i / 10d, [i + 1L])), 100000, 0, token);
        var index = NumericMetadataIndex.Sanitize(data, 100000, 0, token)["iso"];
        var ids = new IdSet();
        Assert.True(NumericRange.TryParse("9999.7-9999.9", false, out var narrow));
        index.Lookup(narrow, ids);
        Assert.Equal(new long[] { 99998, 99999, 100000 }, ids.Order());
        ids.Clear();
        Assert.True(NumericRange.TryParse(">=0", false, out var broad));
        index.Lookup(broad, ids);
        Assert.Equal(100000, ids.Count);
    }

    /// <summary>
    /// ﾃﾞーﾀﾑ作成
    /// </summary>
    /// <returns></returns>
    static SearchIndexDataum JailedDatum() {
        var data = new SearchIndexDataum();
        data.AddNumericField("meta:iso", new Dictionary<double, HashSet<long>> {
            [100] = [1], [400] = [2], [800] = [3],
        }, 10, 0, TestContext.Current.CancellationToken);
        data.AddNumericField("meta:exposure", new Dictionary<double, HashSet<long>> {
            [0.008] = [1], [0.01] = [2],
        }, 10, 0, TestContext.Current.CancellationToken);
        data.AddField("meta:model", new Dictionary<string, HashSet<long>> {
            ["canon"] = [1, 2], ["nikon"] = [3],
        }, 10, 0, TestContext.Current.CancellationToken);
        return data;
    }

    [Fact(DisplayName = "最新のインデックスなら古いエイリアスは無し")]
    public void StaleAliases1() {
        var datum = new ImogeDatum();
        datum.LoadSearchIndices(JailedDatum(), 10L, 0L, TestContext.Current.CancellationToken, Aliases);
        Assert.Empty(datum.StaleMetadataAliases);
    }

    [Fact(DisplayName = "画像が増えて古くなったインデックスのエイリアスを報告する")]
    public void StaleAliases2() {
        var datum = new ImogeDatum();
        datum.LoadSearchIndices(JailedDatum(), 11L, 0L, TestContext.Current.CancellationToken, Aliases);
        Assert.Equal(["iso", "exposure", "model"], datum.StaleMetadataAliases);
    }

    [Fact(DisplayName = "設定にあるのに作られていないエイリアスを報告する")]
    public void StaleAliases3() {
        var data = new SearchIndexDataum();
        data.AddNumericField("meta:iso", new Dictionary<double, HashSet<long>> { [100] = [1] },
            10, 0, TestContext.Current.CancellationToken);
        var datum = new ImogeDatum();
        datum.LoadSearchIndices(data, 10L, 0L, TestContext.Current.CancellationToken, Aliases);
        Assert.Equal(["exposure", "model"], datum.StaleMetadataAliases);
    }

    /// <summary>
    /// ｲﾝﾃﾞｯｸｽからいもげ構築
    /// </summary>
    /// <param name="data"></param>
    /// <returns></returns>
    static ImogeDatum Load(SearchIndexDataum data) {
        var datum = new ImogeDatum();
        datum.LoadSearchIndices(data, 10L, 0L, TestContext.Current.CancellationToken);
        return datum;
    }

    /// <summary>
    /// ｸｴﾘー構築
    /// </summary>
    /// <param name="text"></param>
    /// <param name="datum"></param>
    /// <returns></returns>
    static MurQuery Query(string text, ImogeDatum datum) {
        var query = new MurQuery(Aliases);
        query.Parse(text);
        query.PrepareTag(datum, TestContext.Current.CancellationToken);
        return query;
    }

    /// <summary>
    /// 画像ID
    /// </summary>
    /// <param name="text"></param>
    /// <param name="datum"></param>
    /// <returns></returns>
    static long[] Matches(string text, ImogeDatum datum) {
        var query = Query(text, datum);
        return Enumerable.Range(1, 4).Select(id => new FileItem { Id = id, Width = id == 4 ? 2000 : 640 })
            .Where(item => query.Evaluate(item, TestContext.Current.CancellationToken)).Select(item => item.Id).ToArray();
    }
}
