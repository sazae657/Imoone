using ImogeIndex.Mur;

namespace ImogeIndex.Web2.Mur;

/// <summary>
/// label: 検索
/// </summary>
public class LabelQueryController
{
    [Theory(DisplayName = "ラベルは正規化した上で完全一致")]
    [InlineData("label:お気に入り🍆", "お気に入り🍆", true)]
    [InlineData("label:お気に入り🍆", "お気に入り2", false)]
    [InlineData("label:お気に", "お気に入り", false)]
    [InlineData("label:FAV", "fav", true)]
    [InlineData("label:🧄", "🧄", true)]
    [InlineData("label:\"あとで 見る\"", "あとで 見る", true)]
    [InlineData("label:fav", null, false)]
    public void ExactMatch(string filter, string? label, bool expected)
    {
        Assert.Equal(expected, Evaluate(filter, label));
    }

    [Theory(DisplayName = "label:* はラベル付きの全件")]
    [InlineData("label:*", "fav", true)]
    [InlineData("label:*", null, false)]
    [InlineData("-label:*", "fav", false)]
    [InlineData("-label:*", null, true)]
    [InlineData("-label:fav", "fav", false)]
    [InlineData("-label:fav", "other", true)]
    public void AnyLabel(string filter, string? label, bool expected)
    {
        Assert.Equal(expected, Evaluate(filter, label));
    }

    [Fact(DisplayName = "入力途中のlabel:は無視する")]
    public void EmptyLabelIsIgnored()
    {
        var query = new MurQuery();
        query.Parse("label:");
        Assert.True(query.IsEmpty());
    }

    [Fact(DisplayName = "ORで繋げる")]
    public void OrJoin()
    {
        Assert.True(Evaluate("label:a | label:b", "b"));
        Assert.False(Evaluate("label:a | label:b", "c"));
    }

    [Fact(DisplayName = "特定ラベルは label:* より狭い")]
    public void Narrower()
    {
        var any = new MurQuery();
        any.Parse("label:*");
        var fav = new MurQuery();
        fav.Parse("label:fav");
        Assert.True(fav.IsNarrowerThan(any));
        Assert.False(any.IsNarrowerThan(fav));
    }

    [Fact(DisplayName = "ラベルの書き換えが検索に反映される")]
    public void FollowsRelabel()
    {
        var query = new MurQuery();
        query.Parse("label:fav");
        var item = new FileItem { Id = 1, Label = "other" };
        Assert.False(query.Evaluate(item, CancellationToken.None));
        item.Label = "fav";
        Assert.True(query.Evaluate(item, CancellationToken.None));
        item.Label = "";
        Assert.Null(item.Label);
        Assert.False(query.Evaluate(item, CancellationToken.None));
    }

    private static bool Evaluate(string filter, string? label)
    {
        var query = new MurQuery();
        query.Parse(filter);
        var item = new FileItem { Id = 1, Label = label };
        return query.Evaluate(item, CancellationToken.None);
    }
}
