using ImogeIndex.Storage;
using System.Text;

namespace ImogeIndex.Mur;

/// <summary>
/// gramから値ID絞って部分一致を確認して画像IDを解決する
/// </summary>
public sealed class NgramInvertedIndex {
    readonly Dictionary<long, SearchIndexValue> values = [];
    // 値IDは昇順に揃えておく(ﾏーｼﾞで積を取る用)
    readonly Dictionary<string, long[]> grams = new(StringComparer.Ordinal);
    // gram長未満の検索語用に、文字(ｺーﾄﾞﾎﾟｲﾝﾄ)→値IDの表を初回だけ作る
    readonly Lazy<Dictionary<Rune, long[]>> unigrams;
    readonly int gramSize;

    /// <summary>
    /// ひみつｺﾝｽﾄﾗｸﾀー
    /// </summary>
    /// <param name="gramSize"></param>
    NgramInvertedIndex(int gramSize) {
        this.gramSize = gramSize;
        unigrams = new(BuildJailgrams, LazyThreadSafetyMode.ExecutionAndPublication);
    }

    /// <summary>
    /// ｲﾝﾒﾓﾘ検索ｲﾝﾃﾞｯｸｽ構築
    /// </summary>
    /// <param name="data"></param>
    /// <param name="maxImageId"></param>
    /// <param name="metaHash"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    public static Dictionary<string, NgramInvertedIndex> Sanitize(
        SearchIndexDataum data,
        long maxImageId,
        long metaHash,
        CancellationToken token)
    {
        var ret = new Dictionary<string, NgramInvertedIndex>(StringComparer.Ordinal);
        foreach (var w in data.Definitions) {
            token.ThrowIfCancellationRequested();
            if (w.ValueType != ImogeIndex.MetadataValueType.Text || w.Version != SearchIndexDataum.CurrentVersion ||
                w.MaxImageId < maxImageId ||
                w.GramSize != SearchIndexDataum.GramSizeOf(w.Field) ||
                (w.Field.StartsWith("meta:", StringComparison.Ordinal) && w.ConfigurationHash != metaHash)
            )
            {
                // 変なｴﾝﾄﾘーはｶﾞﾝ無視
                continue;
            }
            ret.Add(w.Field, new NgramInvertedIndex(w.GramSize));
        }
        foreach (var x in data.Values) {
            token.ThrowIfCancellationRequested();
            if (ret.TryGetValue(x.Field, out var index)) {
                index.values.Add(x.ValueId, x);
            }
        }
        foreach (var x in data.Grams) {
            token.ThrowIfCancellationRequested();
            if (ret.TryGetValue(x.Field, out var index)) {
                index.grams.Add(x.Gram, SortedDistinct(x.ValueIds));
            }
        }
        return ret;
    }

    /// <summary>
    /// 画像IDの解決
    /// </summary>
    /// <param name="term"></param>
    /// <param name="dest"></param>
    public void Lookup(string term, IdSet dest) {
        if (term.Length == 0) {
            foreach (var n in values.Values) {
                dest.AddRange(n.ImageIds);
            }
            return;
        }

        // gram長はｺーﾄﾞﾎﾟｲﾝﾄ単位なので、検索語もｺーﾄﾞﾎﾟｲﾝﾄで数える
        var length = SearchIndexDataum.CodePointLength(term);
        var candidate = length < gramSize ? LookupChars(term) : LookupGrams(term);
        if (candidate is null) {
            return;
        }
        // 1文字なら文字表に載ってる時点で一致確定
        var verify = length > 1;
        foreach (var x in candidate) {
            if (values.TryGetValue(x, out var value) &&
                (!verify || value.Value.Contains(term, StringComparison.Ordinal))) {
                dest.AddRange(value.ImageIds);
            }
        }
    }

    /// <summary>
    /// gramで候補の値IDを絞る
    /// </summary>
    /// <param name="term"></param>
    /// <returns>一致なしならnull</returns>
    private long[]? LookupGrams(string term) {
        var lists = new List<long[]>();
        foreach (var m in SearchIndexDataum.InternetGrams(term, gramSize)) {
            if (!grams.TryGetValue(m, out var ids)) {
                return null;
            }
            lists.Add(ids);
        }
        return IntersectAll(lists);
    }

    /// <summary>
    /// 文字単位で候補の値IDを絞る(gram長未満の検索語用)
    /// </summary>
    /// <param name="term"></param>
    /// <returns>一致なしならnull</returns>
    private long[]? LookupChars(string term) {
        var table = unigrams.Value;
        var lists = new List<long[]>(term.Length);
        foreach (var c in term.EnumerateRunes().Distinct()) {
            if (!table.TryGetValue(c, out var ids)) {
                return null;
            }
            lists.Add(ids);
        }
        return IntersectAll(lists);
    }

    /// <summary>
    /// 文字→値IDの表を作る
    /// </summary>
    /// <returns></returns>
    private Dictionary<Rune, long[]> BuildJailgrams() {
        var table = new Dictionary<Rune, List<long>>();
        var seen = new HashSet<Rune>();
        foreach (var (valueId, value) in values.OrderBy(x => x.Key)) {
            seen.Clear();
            foreach (var c in value.Value.EnumerateRunes()) {
                if (!seen.Add(c)) {
                    continue;
                }
                if (!table.TryGetValue(c, out var list)) {
                    table.Add(c, list = []);
                }
                list.Add(valueId);
            }
        }
        return table.ToDictionary(x => x.Key, x => x.Value.ToArray());
    }

    /// <summary>
    /// 昇順の値ID列の積
    /// </summary>
    /// <param name="lists"></param>
    /// <returns>空ならnull</returns>
    private static long[]? IntersectAll(List<long[]> lists) {
        if (lists.Count == 0) {
            return null;
        }
        // 短い方から積を取ると早く縮む
        lists.Sort(static (a, b) => a.Length.CompareTo(b.Length));
        var candidate = lists[0];
        for (var i = 1; i < lists.Count && candidate.Length != 0; i++) {
            candidate = Intersect(candidate, lists[i]);
        }
        return candidate.Length == 0 ? null : candidate;
    }

    /// <summary>
    /// 昇順配列同士の積(ﾏーｼﾞ)
    /// </summary>
    /// <param name="a"></param>
    /// <param name="b"></param>
    /// <returns></returns>
    private static long[] Intersect(long[] a, long[] b) {
        var result = new List<long>(Math.Min(a.Length, b.Length));
        int i = 0, j = 0;
        while (i < a.Length && j < b.Length) {
            if (a[i] < b[j]) {
                i++;
            }
            else if (a[i] > b[j]) {
                j++;
            }
            else {
                result.Add(a[i]);
                i++;
                j++;
            }
        }
        return result.ToArray();
    }

    /// <summary>
    /// 昇順かつ重複なしに揃える(既に揃ってたらそのまま)
    /// </summary>
    /// <param name="ids"></param>
    /// <returns></returns>
    private static long[] SortedDistinct(long[] ids) {
        for (var i = 1; i < ids.Length; i++) {
            if (ids[i - 1] >= ids[i]) {
                return ids.Distinct().Order().ToArray();
            }
        }
        return ids;
    }
}
