
namespace ImogeIndex.Mur;

/// <summary>
/// 検索
/// </summary>
internal interface IQueryTerm
{
    /// <summary>
    /// ﾏｯﾁｮ
    /// </summary>
    /// <param name="item"></param>
    /// <returns></returns>
    bool IsMatch(FileItem item);

    /// <summary>
    /// 大まかな評価ｺｽﾄ
    /// </summary>
    int Cost { get; }


    /// <summary>
    /// 転置ｲﾝﾃﾞｯｸｽで候補IDを試行
    /// 解決できた分をdestinationに足す(falseを返す時はdestinationに触らないこと)
    /// </summary>
    /// <param name="datum"></param>
    /// <param name="destination"></param>
    /// <returns></returns>
    bool TryResolveIds(ImogeDatum datum, IdSet destination);


    /// <summary>
    /// この条件に一致する項目がotherにも一致する？
    /// </summary>
    /// <param name="other"></param>
    /// <returns></returns>
    bool IsNarrowerThan(IQueryTerm other);
}

/// <summary>
/// 不正な検索条件
/// 黙って捨てると結果が広がるので、何にも一致しない条件として残す
/// </summary>
internal sealed class InvalidQueryTerm : IQueryTerm
{
    public static readonly InvalidQueryTerm Instance = new();

    InvalidQueryTerm() { }

    public int Cost => 0;

    public bool IsMatch(FileItem item) => false;

    // 空集合として解決済み
    public bool TryResolveIds(ImogeDatum datum, IdSet destination) => true;

    // 空集合=全ての集合の部分集合という事にしとく
    public bool IsNarrowerThan(IQueryTerm other) => true;
}

/// <summary>
/// 否定条件
/// </summary>
internal sealed class NotQueryTerm : IQueryTerm
{
    readonly IQueryTerm inner;

    NotQueryTerm(IQueryTerm inner) => this.inner = inner;

    /// <summary>
    /// 否定される条件
    /// </summary>
    public IQueryTerm Inner => inner;

    /// <summary>
    /// 否定条件を作る
    /// 不正な条件を否定すると全件一致で結果が広がるので、不正な条件のまま返す
    /// </summary>
    /// <param name="inner"></param>
    /// <returns></returns>
    public static IQueryTerm Sanitize(IQueryTerm inner) => inner switch {
        InvalidQueryTerm => inner,
        NotQueryTerm not => not.inner,
        _ => new NotQueryTerm(inner),
    };

    public int Cost => inner.Cost;

    public bool IsMatch(FileItem item) => !inner.IsMatch(item);

    // 補集合はdestinationに足せないので解決しない
    // 単独の否定ｸﾞﾙーﾌﾟはMurQuery側で除外IDとして扱わせとく
    public bool TryResolveIds(ImogeDatum datum, IdSet destination) => false;

    public bool IsNarrowerThan(IQueryTerm other) =>
        // !A ⊆ !B ⇔ B ⊆ A なので包含関係が逆向きになる事注意
        other is NotQueryTerm o && o.inner.IsNarrowerThan(inner);
}

/// <summary>
///  その文字列含まれてる検索
/// </summary>
internal sealed class ContainsTextQueryTerm : IQueryTerm
{
    readonly string value;
    readonly SearchField[] fields;
    readonly int cost;
    readonly bool indexable;

    public ContainsTextQueryTerm(string value, SearchField[] fields)
    {
        // 対象側を小文字で保持しているので検索語も揃えてOrdinalで比較
        this.value = FileItem.Normalize(value);
        this.fields = fields;
        var total = 0;
        foreach (var field in fields) {
            total += FieldCost(field);
        }
        cost = total;
        indexable = fields.Length == 1
            && this.value.IndexOf(',') < 0
            && this.value.Length == this.value.Trim().Length;
    }

    public int Cost => cost;

    public bool IsMatch(FileItem item)
    {
        for (var i = 0; i < fields.Length; i++) {
            var candidate = item.GetSearchText(fields[i]);
            if (candidate.Length != 0 &&
                candidate.Contains(value, StringComparison.Ordinal)) {
                return true;
            }
        }
        return false;
    }

    public bool TryResolveIds(ImogeDatum datum, IdSet destination)
    {
        if (!indexable || !datum.TryGetIndex(fields[0], out var index)) {
            return false;
        }
        index.Lookup(value, destination);
        return true;
    }

    public bool IsNarrowerThan(IQueryTerm other)
    {
        if (other is not ContainsTextQueryTerm o || o.fields.Length != fields.Length) {
            return false;
        }
        for (var i = 0; i < fields.Length; i++) {
            if (fields[i] != o.fields[i]) {
                return false;
            }
        }
        return value.Contains(o.value, StringComparison.Ordinal);
    }


    /// <summary>
    /// 指定ﾌｨーﾙﾄﾞごとの走査ｺｽﾄの目安
    /// </summary>
    /// <param name="field"></param>
    /// <returns></returns>
    private static int FieldCost(SearchField field) => field switch {
        SearchField.Name => 1,
        SearchField.Path => 2,
        SearchField.Character => 2,
        SearchField.Costume => 3,
        SearchField.Scene => 4,
        // Tag/Keyword/Objects はｶﾝﾏ区切りでﾃﾞｶいので最重量
        _ => 8,
    };
}
