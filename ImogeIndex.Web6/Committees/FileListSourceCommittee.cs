using ImogeIndex.Mur;

namespace ImogeIndex.Web6.Committees;

/// <summary>
/// 一覧に流す項目列製造
/// ListCollectionViewのFilter/SortDescriptionsはUIｽﾚｯﾄﾞで全件回るので、
/// ｿーﾄと絞り込みはｽﾚﾌﾟで済ませて出来上がった配列だけをUIに渡す
/// </summary>
public sealed class FileListSourceCommittee
{
    IReadOnlyList<FileItem> items = Array.Empty<FileItem>();
    IComparer<FileItem>? comparer;
    IdSet? filter;

    // ｿーﾄ済み全件(itemsとcomparerの組み合わせごとに1回だけ作る)
    IReadOnlyList<FileItem>? sortedSource;
    IComparer<FileItem>? sortedComparer;
    Task<FileItem[]> sortTask = Task.FromResult(Array.Empty<FileItem>());

    int version;

    /// <summary>
    /// 読み込み順の全件
    /// </summary>
    public IReadOnlyList<FileItem> Items {
        get => items;
        set => items = value;
    }

    /// <summary>
    /// 並び順(nullなら読み込み順)
    /// </summary>
    public IComparer<FileItem>? Comparer {
        get => comparer;
        set => comparer = value;
    }

    /// <summary>
    /// 表示するID(nullなら全件)
    /// </summary>
    public IdSet? Filter {
        get => filter;
        set => filter = value;
    }

    /// <summary>
    /// 現在の条件で表示に残る？
    /// </summary>
    /// <param name="item"></param>
    /// <returns></returns>
    public bool IsVisible(FileItem item) => RankedItems is { } ranked
        ? ranked.Any(x => x.Id == item.Id)
        : filter is null || filter.Contains(item.Id);

    /// <summary>
    /// 類似検索の順位付き結果
    /// </summary>
    public IReadOnlyList<FileItem>? RankedItems { get; set; }

    /// <summary>
    /// 実行中の一覧構築を古い要求として無効にする
    /// </summary>
    public void Invalidate() => version++;

    /// <summary>
    /// 現在の条件で表示用のﾃﾞーﾀﾑを作り直す
    /// 後から来た要求に追い越されたらnull
    /// </summary>
    /// <param name="token"></param>
    /// <returns></returns>
    public async Task<IReadOnlyList<FileItem>?> BuildAsync(CancellationToken token)
    {
        var ver = ++version;
        token.ThrowIfCancellationRequested();
        if (RankedItems is { } ranked) {
            return ranked;
        }
        var ids = filter;
        var sorted = await EnsureSorted().WaitAsync(token);
        IReadOnlyList<FileItem> result = ids is null
            ? sorted
            : await Task.Run(() => Extract(sorted, ids, token), token);
        return ver == version ? result : null;
    }

    /// <summary>
    /// ｿーﾄ済み全件を取得(条件が変わってたらｿーﾄし直す)
    /// </summary>
    /// <returns></returns>
    private Task<FileItem[]> EnsureSorted()
    {
        if (!ReferenceEquals(sortedSource, items) || !ReferenceEquals(sortedComparer, comparer)) {
            var src = items;
            var cmp = comparer;
            sortedSource = src;
            sortedComparer = cmp;
            // ｿーﾄは他の要求と共有するのでｷｬﾝｾﾙさせない
            sortTask = Task.Run(() => Sort(src, cmp));
        }
        return sortTask;
    }

    /// <summary>
    /// ｿーﾄ
    /// </summary>
    /// <param name="source"></param>
    /// <param name="cmp"></param>
    /// <returns></returns>
    private static FileItem[] Sort(IReadOnlyList<FileItem> source, IComparer<FileItem>? cmp)
    {
        var array = source.ToArray();
        if (cmp is null) {
            return array;
        }
        // Array.Sortは安定ｿーﾄでないので、同値は元の並び順で決める
        var keys = new int[array.Length];
        for (var i = 0; i < keys.Length; i++) {
            keys[i] = i;
        }
        Array.Sort(keys, (a, b) => {
            var c = cmp.Compare(array[a], array[b]);
            return c != 0 ? c : a.CompareTo(b);
        });
        var result = new FileItem[array.Length];
        for (var i = 0; i < keys.Length; i++) {
            result[i] = array[keys[i]];
        }
        return result;
    }

    /// <summary>
    /// 抽出
    /// </summary>
    /// <param name="sorted"></param>
    /// <param name="ids"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    private static FileItem[] Extract(FileItem[] sorted, IdSet ids, CancellationToken token)
    {
        var result = new List<FileItem>(Math.Min(ids.Count, sorted.Length));
        for (var i = 0; i < sorted.Length; i++) {
            if ((i & 0xFFF) == 0) {
                token.ThrowIfCancellationRequested();
            }
            if (ids.Contains(sorted[i].Id)) {
                result.Add(sorted[i]);
            }
        }
        return result.ToArray();
    }
}
