
namespace ImogeIndex.Mur;

/// <summary>
/// 検索結果
/// </summary>
public sealed class MurSearchResult
{
    /// <summary>
    /// 条件に一致した項目
    /// </summary>
    public required List<FileItem> Items { get; init; }

    /// <summary>
    /// 一致した項目のID
    /// </summary>
    public required IdSet Ids { get; init; }
}

/// <summary>
/// 検索ｷｬー
/// </summary>
public sealed class MurSearcher
{
    /// <summary>
    /// これ未満の件数は分割ｺｽﾄの方が高いので直列で回す
    /// </summary>
    const int ParallelThreshold = 20000;

    MurQuery? lastQuery;
    List<FileItem>? lastEitems;
    // lastItemsを作った時のﾃﾞーﾀﾑ世代
    object? lastVersion;

    /// <summary>
    /// ﾘｾｯｼｭ
    /// </summary>
    public void Reset()
    {
        lastQuery = null;
        lastEitems = null;
        lastVersion = null;
    }


    /// <summary>
    /// ｽﾚﾌﾟで検索を回す
    /// </summary>
    /// <param name="query"></param>
    /// <param name="datum"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    public async Task<MurSearchResult> SearchAsync(
        MurQuery query, ImogeDatum datum, CancellationToken token)
    {
        // 再読み込みと競合すると面倒くせえので、項目とｲﾝﾃﾞｯｸｽは同じ世代を最後まで使う
        var snapshot = datum.Capture();
        var version = snapshot.Version;
        var source = lastQuery is not null && lastEitems is not null &&
            ReferenceEquals(lastVersion, version) && query.IsNarrowerThan(lastQuery)
            ? lastEitems
            : snapshot.Items;

        var result = await Task.Run(() => Run(query, snapshot, source, token), token);
        token.ThrowIfCancellationRequested();
        lastQuery = query;
        lastEitems = result.Items;
        lastVersion = version;
        return result;
    }

    /// <summary>
    /// 検索
    /// </summary>
    /// <param name="query"></param>
    /// <param name="datum"></param>
    /// <param name="src"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    private static MurSearchResult Run(
        MurQuery query, ImogeDatum datum, IReadOnlyList<FileItem> src, CancellationToken token)
    {
        // 転置ｲﾝﾃﾞｯｸｽの解決はここで済ませとく
        query.PrepareTag(datum, token);
        token.ThrowIfCancellationRequested();

        var eitems = src.Count < ParallelThreshold || !query.RequiresTextScan
            ? RunSerial(query, src, token)
            : RunParallel(query, src, token);

        var ids = new IdSet();
        foreach (var x in eitems) {
            ids.Add(x.Id);
        }
        return new MurSearchResult { Items = eitems, Ids = ids };
    }

    /// <summary>
    /// 普通に検索
    /// </summary>
    /// <param name="query"></param>
    /// <param name="source"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    private static List<FileItem> RunSerial(
        MurQuery query, IReadOnlyList<FileItem> source, CancellationToken token)
    {
        var eitems = new List<FileItem>();
        for (var i = 0; i < source.Count; i++) {
            if ((i & 0xFFF) == 0) {
                token.ThrowIfCancellationRequested();
            }
            if (query.Evaluate(source[i], token)) {
                eitems.Add(source[i]);
            }
        }
        return eitems;
    }

    /// <summary>
    /// ParallelThreshold超えの検索
    /// </summary>
    /// <param name="query"></param>
    /// <param name="source"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    private static List<FileItem> RunParallel(
        MurQuery query, IReadOnlyList<FileItem> source, CancellationToken token)
    {
        var part = Math.Max(2, Environment.ProcessorCount);
        var spam = (source.Count + part - 1) / part;
        var chunks = new List<FileItem>[part];
        var opt = new ParallelOptions {
            CancellationToken = token,
            MaxDegreeOfParallelism = part,
        };

        Parallel.For(0, part, opt, p => {
            var start = p * spam;
            var end = Math.Min(start + spam, source.Count);
            var ck = new List<FileItem>();
            for (var i = start; i < end; i++) {
                if ((i & 0xFFF) == 0) {
                    opt.CancellationToken.ThrowIfCancellationRequested();
                }
                if (query.Evaluate(source[i], token)) {
                    ck.Add(source[i]);
                }
            }
            chunks[p] = ck;
        });

        // 元の並び順を保ったまま連結
        var total = 0;
        foreach (var bucket in chunks) {
            total += bucket?.Count ?? 0;
        }
        var eitems = new List<FileItem>(total);
        foreach (var bucket in chunks) {
            if (bucket is not null) {
                eitems.AddRange(bucket);
            }
        }
        return eitems;
    }
}
