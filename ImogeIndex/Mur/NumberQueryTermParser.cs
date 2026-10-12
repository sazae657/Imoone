using ImogeIndex.Common;

namespace ImogeIndex.Mur;

/// <summary>
/// 数値条件
/// </summary>
internal sealed class NumberQueryTerm(Func<FileItem, int> selector, NumericRange range) : IQueryTerm {
    public int Cost => 1;
    public bool IsMatch(FileItem item) => range.Contains(selector(item));
    public bool TryResolveIds(ImogeDatum datum, IdSet destination) => false;
    public bool IsNarrowerThan(IQueryTerm other) => other is NumberQueryTerm number
        && selector == number.Selector && range.IsSubsetOf(number.Range);
    private Func<FileItem, int> Selector => selector;
    private NumericRange Range => range;
}

/// <summary>
/// 数値条件のﾊﾟーｻー
/// </summary>
/// <param name="prefix"></param>
/// <param name="selector"></param>
internal sealed class NumberQueryTermParser(string prefix, Func<FileItem, int> selector) : IQueryTermParser {
    public bool TryParse(string token, out IQueryTerm? term) {
        term = null;
        if (!token.StartsWith(prefix, StringComparison.Ordinal)) {
            return false;
        }
        // 解釈できない数値は捨てずに一致なしにする
        term = NumericRange.TryParse(token[prefix.Length..], true, out var range)
            ? new NumberQueryTerm(selector, range)
            : InvalidQueryTerm.Instance;
        return true;
    }
}
