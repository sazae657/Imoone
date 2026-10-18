namespace ImogeIndex.Mur;

/// <summary>
/// label:ﾗﾍﾞﾙ 形式のﾕーｻﾞー定義ﾗﾍﾞﾙ検索条件
/// label:* でﾗﾍﾞﾙ付きの全件
/// </summary>
internal sealed class LabelQueryTermParser : IQueryTermParser
{
    const string Prefix = "label:";
    const string AnyLabel = "*";

    public bool TryParse(string token, out IQueryTerm? term)
    {
        if (!token.StartsWith(Prefix, StringComparison.Ordinal)) {
            term = null;
            return false;
        }
        var value = Unquote(token[Prefix.Length..]).Trim();
        if (value.Length == 0) {
            // 入力途中
            term = null;
            return true;
        }
        term = value == AnyLabel
            ? new LabelQueryTerm(null)
            : new LabelQueryTerm(FileItem.NormalizeLabel(value));
        return true;
    }

    private static string Unquote(string value)
    {
        if (value.Length >= 2 && value[0] == '"' && value[^1] == '"') {
            return value[1..^1];
        }
        return value;
    }
}

/// <summary>
/// ﾗﾍﾞﾙの完全一致条件
/// ﾗﾍﾞﾙは後から書き換わるので転置ｲﾝﾃﾞｯｸｽは作らずFileItemを直接見る
/// </summary>
/// <param name="label">正規化済みのﾗﾍﾞﾙ、nullならﾗﾍﾞﾙ付きなら何でも</param>
internal sealed class LabelQueryTerm(string? label) : IQueryTerm
{
    public int Cost => 1;

    string? Label => label;

    public bool IsMatch(FileItem item)
    {
        var key = item.LabelKey;
        if (key == null) {
            return false;
        }
        return label == null || string.Equals(key, label, StringComparison.Ordinal);
    }

    public bool TryResolveIds(ImogeDatum datum, IdSet destination) => false;

    public bool IsNarrowerThan(IQueryTerm other)
    {
        if (other is not LabelQueryTerm o) {
            return false;
        }
        // 何でも(*)は特定ﾗﾍﾞﾙを全部含む
        return o.Label == null || string.Equals(label, o.Label, StringComparison.Ordinal);
    }
}
