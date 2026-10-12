using ImogeIndex.Mur;

namespace ImogeIndex.Web2.Mur;

/// <summary>
/// mur
/// </summary>
public class MurQueryController
{
    [Theory(DisplayName ="日付フォーマットのテスツ")]
    [InlineData("dc:2015", "2015-12-31T23:59:59", true)]
    [InlineData("dc:2015", "2016-01-01T00:00:00", false)]
    [InlineData("dc:2015-10", "2015-10-31T23:59:59", true)]
    [InlineData("dc:2015-10-21", "2015-10-21T23:59:59", true)]
    [InlineData("dc:2015-10-21T16", "2015-10-21T16:59:59", true)]
    [InlineData("dc:2015-10-21T16:29", "2015-10-21T16:29:59", true)]
    [InlineData("dc:2015-10-21T16:29:01", "2015-10-21T16:29:01", true)]
    [InlineData("dc:2015-10-21T16:29:01", "2015-10-21T16:29:02", false)]
    public void DatePrecision(string filter, string itemDate, bool expected)
    {
        Assert.Equal(expected, Evaluate(filter, itemDate));
    }

    [Theory(DisplayName = "条件フォーマットのテスツ")]
    [InlineData("dc:>2015-10", "2015-11-01T00:00:00", true)]
    [InlineData("dc:>2015-10", "2015-10-31T23:59:59", false)]
    [InlineData("dc:>=2015-10", "2015-10-01T00:00:00", true)]
    [InlineData("dc:<2015-10", "2015-09-30T23:59:59", true)]
    [InlineData("dc:<2015-10", "2015-10-01T00:00:00", false)]
    [InlineData("dc:<=2015-10", "2015-10-31T23:59:59", true)]
    [InlineData("dc:<=2015-10", "2015-11-01T00:00:00", false)]
    public void DateComparison(string filter, string itemDate, bool expected)
    {
        Assert.Equal(expected, Evaluate(filter, itemDate));
    }

    [Theory(DisplayName = "期間指定フォーマットのテスツ")]
    [InlineData("dc:2015-10-2016-02", "2015-10-01T00:00:00", true)]
    [InlineData("dc:2015-10-2016-02", "2016-02-29T23:59:59", true)]
    [InlineData("dc:2015-10-2016-02", "2016-03-01T00:00:00", false)]
    [InlineData("dc:2015-10-21-2015-10-23", "2015-10-23T23:59:59", true)]
    public void DateRange(string filter, string itemDate, bool expected)
    {
        Assert.Equal(expected, Evaluate(filter, itemDate));
    }

    [Theory(DisplayName = "変な日付は一致なし")]
    [InlineData("dc:2015-02-30")]
    [InlineData("dc:2020-13")]
    [InlineData("dc:abc")]
    public void InvalidDateMatchesNothing(string filter)
    {
        var query = new MurQuery();

        query.Parse(filter);

        Assert.False(query.IsEmpty());
        Assert.False(Evaluate(filter, "2015-02-28T00:00:00"));
    }

    [Theory(DisplayName = "不正な条件で結果が広がらない")]
    // a | 不正 b は (a OR 不正) AND b
    [InlineData("foo | dc:bad bar", "foo", false)]
    [InlineData("foo | dc:bad bar", "bar", false)]
    [InlineData("foo | dc:bad bar", "foobar", true)]
    // 不正な条件単体は一致なし
    [InlineData("foo width:abc", "foo", false)]
    // 入力途中の空条件は無視するが、直前の|で後ろの条件とORにしない
    [InlineData("foo | tag: bar", "foo", false)]
    [InlineData("foo | tag: bar", "foobar", true)]
    public void InvalidTermDoesNotWidenResult(string filter, string name, bool expected)
    {
        var query = new MurQuery();
        query.Parse(filter);
        var item = new FileItem {
            Name = name,
            DateTime = new DateTime(2015, 1, 1),
        };

        Assert.Equal(expected, query.Evaluate(item, CancellationToken.None));
    }

    [Fact(DisplayName = "不正な条件の検索は直前の結果の絞り込みとして扱える")]
    public void InvalidTermIsNarrower()
    {
        var previous = new MurQuery();
        previous.Parse("dc:2015");
        var current = new MurQuery();
        current.Parse("dc:2015-x");
        var next = new MurQuery();
        next.Parse("dc:2015-12");

        Assert.True(current.IsNarrowerThan(previous));
        // 空集合から広げ直す時は絞り込み扱いしない
        Assert.False(next.IsNarrowerThan(current));
    }

    [Theory(DisplayName = "幅の条件フォーマットのテスツ")]
    [InlineData("width:1000", 1000, true)]
    [InlineData("width:1000", 999, false)]
    [InlineData("width:>1000", 1000, false)]
    [InlineData("width:>1000", 1001, true)]
    [InlineData("width:>=1000", 1000, true)]
    [InlineData("width:<1000", 1000, false)]
    [InlineData("width:<1000", 999, true)]
    [InlineData("width:<=1000", 1000, true)]
    [InlineData("width:640-1280", 640, true)]
    [InlineData("width:640-1280", 1280, true)]
    [InlineData("width:640-1280", 1281, false)]
    public void WidthComparison(string filter, int width, bool expected)
    {
        Assert.Equal(expected, EvaluateSize(filter, width, 0));
    }

    [Theory(DisplayName = "高さの条件フォーマットのテスツ")]
    [InlineData("height:720", 720, true)]
    [InlineData("height:>720", 720, false)]
    [InlineData("height:>=720", 720, true)]
    [InlineData("height:<720", 719, true)]
    [InlineData("height:<=720", 721, false)]
    [InlineData("height:480-1080", 1080, true)]
    public void HeightComparison(string filter, int height, bool expected)
    {
        Assert.Equal(expected, EvaluateSize(filter, 0, height));
    }

    [Theory(DisplayName = "不正な数値条件は一致なし")]
    [InlineData("width:")]
    [InlineData("width:abc")]
    [InlineData("width:1280-640")]
    [InlineData("height:1-2-3")]
    public void InvalidNumberMatchesNothing(string filter)
    {
        var query = new MurQuery();

        query.Parse(filter);

        Assert.False(query.IsEmpty());
        Assert.False(EvaluateSize(filter, 1000, 1000));
    }

    [Theory(DisplayName = "否定条件のテスツ")]
    [InlineData("-foo", "bar", true)]
    [InlineData("-foo", "foobar", false)]
    [InlineData("bar -foo", "bar", true)]
    [InlineData("bar -foo", "foobar", false)]
    [InlineData("-\"foo bar\"", "foo bar", false)]
    [InlineData("-\"foo bar\"", "foo", true)]
    // ORの中の否定
    [InlineData("foo | -bar", "foo bar", true)]
    [InlineData("foo | -bar", "bar", false)]
    [InlineData("foo | -bar", "baz", true)]
    // ｷー付きの否定
    [InlineData("-width:>1000", "foo", true)]
    // ｸｫーﾄすれば-で始まる語を検索できる
    [InlineData("\"-foo\"", "a-foo", true)]
    [InlineData("\"-foo\"", "foo", false)]
    // 語中の-は否定ではない
    [InlineData("foo-bar", "foo-bar", true)]
    [InlineData("foo-bar", "foo", false)]
    public void Negation(string filter, string name, bool expected)
    {
        var query = new MurQuery();
        query.Parse(filter);
        var item = new FileItem {
            Name = name,
            Width = 500,
        };

        Assert.Equal(expected, query.Evaluate(item, CancellationToken.None));
    }

    [Theory(DisplayName = "入力途中の-だけの条件は無視")]
    [InlineData("-")]
    [InlineData("foo -")]
    [InlineData("foo - ")]
    public void LoneMinusIsIgnored(string filter)
    {
        var query = new MurQuery();
        query.Parse(filter);

        Assert.True(query.Evaluate(new FileItem { Name = "foo" }, CancellationToken.None));
    }

    [Fact(DisplayName = "不正な条件を否定しても結果が広がらない")]
    public void NegatedInvalidTermMatchesNothing()
    {
        var query = new MurQuery();
        query.Parse("-dc:abc");

        Assert.False(query.IsEmpty());
        Assert.False(query.Evaluate(new FileItem { Name = "foo" }, CancellationToken.None));
    }

    [Theory(DisplayName = "否定条件の絞り込み判定")]
    // 否定語を伸ばすと除外が緩むので結果が広がる
    [InlineData("-fo", "-foo", false)]
    // 否定語を縮めると除外が強まる
    [InlineData("-foo", "-fo", true)]
    [InlineData("foo", "foo -bar", true)]
    [InlineData("foo -bar", "foo", false)]
    [InlineData("foo", "-foo", false)]
    [InlineData("-foo", "foo", false)]
    [InlineData("-tag:foo", "-foo", false)]
    public void NegationIsNarrower(string previousFilter, string currentFilter, bool expected)
    {
        var previous = new MurQuery();
        previous.Parse(previousFilter);
        var current = new MurQuery();
        current.Parse(currentFilter);

        Assert.Equal(expected, current.IsNarrowerThan(previous));
    }

    /// <summary>
    /// 条件の評価
    /// </summary>
    /// <param name="filter"></param>
    /// <param name="itemDate"></param>
    /// <returns></returns>
    private static bool Evaluate(string filter, string itemDate)
    {
        var query = new MurQuery();
        query.Parse(filter);
        var item = new FileItem {
            DateTime = DateTime.Parse(itemDate, System.Globalization.CultureInfo.InvariantCulture),
        };
        return query.Evaluate(item, CancellationToken.None);
    }


    /// <summary>
    /// 幅と高さの条件を評価
    /// </summary>
    /// <param name="filter"></param>
    /// <param name="width"></param>
    /// <param name="height"></param>
    /// <returns></returns>
    private static bool EvaluateSize(string filter, int width, int height)
    {
        var query = new MurQuery();
        query.Parse(filter);
        var item = new FileItem {
            Width = width,
            Height = height,
        };
        return query.Evaluate(item, CancellationToken.None);
    }
}
