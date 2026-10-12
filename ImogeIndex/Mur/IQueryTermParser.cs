
namespace ImogeIndex.Mur;

/// <summary>
/// 検索語ﾊﾟーｻー
/// </summary>
internal interface IQueryTermParser
{
    bool TryParse(string token, out IQueryTerm? term);
}

/// <summary>
/// 条件ｸﾞﾙーﾌﾟ
/// </summary>
internal sealed class QueryGroup
{
    readonly List<IQueryTerm> terms = new();
    readonly List<IQueryTerm> residualTerms = new();
    readonly IdSet resolvedIds = new();
    int cost;
    bool prepared;

    /// <summary>
    /// だいたいのｺｽﾄ
    /// </summary>
    public int Cost => cost;

    /// <summary>
    /// 否定条件1個だけのｸﾞﾙーﾌﾟならその条件
    /// </summary>
    public NotQueryTerm? SoleNegation => terms.Count == 1 ? terms[0] as NotQueryTerm : null;

    public void Add(IQueryTerm term)
    {
        terms.Add(term);
        cost += term.Cost;
    }


    /// <summary>
    /// 安い条件から評価するよう並べ替え
    /// </summary>
    public void SortByCost() => terms.Sort(static (a, b) => a.Cost.CompareTo(b.Cost));

    public bool IsMatch(FileItem item)
    {
        if (prepared && resolvedIds.Contains(item.Id)) {
            return true;
        }
        var targets = prepared ? residualTerms : terms;
        for (var i = 0; i < targets.Count; i++) {
            if (targets[i].IsMatch(item)) {
                return true;
            }
        }
        return false;
    }


    /// <summary>
    /// ｸﾞﾙーﾌﾟ全体を転置ｲﾝﾃﾞｯｸｽで解決
    /// </summary>
    /// <param name="datum"></param>
    /// <param name="destination"></param>
    /// <returns></returns>
    public bool TryResolveIds(ImogeDatum datum, IdSet destination)
    {
        ResetResolution();
        if (terms.Count == 0) {
            return false;
        }
        for (var i = 0; i < terms.Count; i++) {
            // ｸﾞﾙーﾌﾟ内はORなので解決できた分はそのまま足し込む
            if (!terms[i].TryResolveIds(datum, resolvedIds)) {
                residualTerms.Add(terms[i]);
            }
        }
        if (residualTerms.Count == terms.Count) {
            // ｲﾝﾃﾞｯｸｽ解決できなかった場合は通常評価に倒す
            ResetResolution();
            return false;
        }
        prepared = true;
        if (residualTerms.Count != 0) {
            // 一部だけ解決できたORｸﾞﾙーﾌﾟは、項目評価時にID照合と残余条件をOR評価する
            return false;
        }
        destination.UnionWith(resolvedIds);
        ResetResolution();
        return true;
    }


    /// <summary>
    /// ｲﾝﾃﾞｯｸｽ解決で作成した一時状態を破棄
    /// </summary>
    public void ResetResolution()
    {
        residualTerms.Clear();
        resolvedIds.Clear();
        prepared = false;
    }


    /// <summary>
    /// このｸﾞﾙーﾌﾟはotherの部分集合？
    /// </summary>
    /// <param name="other"></param>
    /// <returns></returns>
    public bool IsNarrowerThan(QueryGroup other)
    {
        if (terms.Count == 0) {
            return false;
        }
        for (var i = 0; i < terms.Count; i++) {
            var covered = false;
            for (var j = 0; j < other.terms.Count; j++) {
                if (terms[i].IsNarrowerThan(other.terms[j])) {
                    covered = true;
                    break;
                }
            }
            if (!covered) {
                return false;
            }
        }
        return true;
    }
}
