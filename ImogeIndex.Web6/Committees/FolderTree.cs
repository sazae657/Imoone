using ImogeIndex.Mur;
using ImogeIndex.Sources;
using ImogeIndex.Web6.Modelle;
using System.Runtime.InteropServices;

namespace ImogeIndex.Web6.Committees;

/// <summary>
/// ﾌｫﾙﾀﾞーﾂﾘー
/// 最上位はﾌﾟﾛﾌｧｲﾙ名、その下に走査ﾙーﾄ、さらにその下にｻﾌﾞﾌｫﾙﾀﾞー
/// </summary>
public sealed class FolderTree
{
    readonly FolderNode[] nodes;
    readonly int[] parents;
    readonly Dictionary<string, FolderNode> locations;

    private FolderTree(FolderNode root, FolderNode[] orderedNodes, int[] parentOrders, Dictionary<string, FolderNode> locationMap)
    {
        Root = root;
        nodes = orderedNodes;
        parents = parentOrders;
        locations = locationMap;
    }

    /// <summary>
    /// 最上位(ﾌﾟﾛﾌｧｲﾙ名)
    /// </summary>
    public FolderNode Root { get; }

    /// <summary>
    /// 節の数(最上位含む)
    /// </summary>
    public int Count => nodes.Length;

    /// <summary>
    /// 場所から節を引く(null/空なら最上位、無ければnull)
    /// </summary>
    /// <param name="location"></param>
    /// <returns></returns>
    public FolderNode? Find(string? location)
    {
        if (string.IsNullOrEmpty(location)) {
            return Root;
        }
        return locations.GetValueOrDefault(location);
    }

    /// <summary>
    /// 項目の入っている節の番号(ﾂﾘーに無ければ-1)
    /// </summary>
    /// <param name="item"></param>
    /// <returns></returns>
    internal int OrderOf(FileItem item) => locations.TryGetValue(item.Path, out var node) ? node.Order : -1;

    /// <summary>
    /// 節の配下で絞り込む条件(最上位やnullなら絞り込まないのでnull)
    /// </summary>
    /// <param name="node"></param>
    /// <returns></returns>
    public FolderScope? SanitizeScope(FolderNode? node)
    {
        if (node == null || node.IsProfileRoot || !ReferenceEquals(nodes.ElementAtOrDefault(node.Order), node)) {
            return null;
        }
        return new FolderScope(this, node);
    }

    /// <summary>
    /// 節を選択して見えるように親を展開する
    /// </summary>
    /// <param name="node"></param>
    public static void Reveal(FolderNode node)
    {
        for (var p = node.Parent; p != null; p = p.Parent) {
            p.IsExpanded = true;
        }
        node.IsSelected = true;
    }

    /// <summary>
    /// 表示中の項目から節ごとの件数(配下込み)を数える
    /// </summary>
    /// <param name="items"></param>
    /// <param name="token"></param>
    /// <returns>節の番号ごとの件数</returns>
    public int[] ComputeHits(IReadOnlyList<FileItem> items, CancellationToken token)
    {
        var hits = new int[nodes.Length];
        string? lastPath = null;
        var lastOrder = -1;
        for (var i = 0; i < items.Count; i++) {
            if ((i & 0xFFF) == 0) {
                token.ThrowIfCancellationRequested();
            }
            // 直前と同じなら引き直さない
            var path = items[i].Path;
            if (!ReferenceEquals(path, lastPath)) {
                lastPath = path;
                lastOrder = locations.TryGetValue(path, out var node) ? node.Order : -1;
            }
            if (lastOrder >= 0) {
                hits[lastOrder]++;
            }
        }
        for (var i = hits.Length - 1; i > 0; i--) {
            hits[parents[i]] += hits[i];
        }
        return hits;
    }

    /// <summary>
    /// 件数を節に反映して、0件の節を隠す
    /// </summary>
    /// <param name="hits"></param>
    public void ApplyHits(int[] hits)
    {
        if (hits.Length != nodes.Length) {
            throw new ArgumentException(null, nameof(hits));
        }
        for (var i = 0; i < nodes.Length; i++) {
            var node = nodes[i];
            node.HitCount = hits[i];
            node.IsVisible = node.IsProfileRoot || hits[i] > 0;
        }
    }

    /// <summary>
    /// 空のﾂﾘー
    /// </summary>
    /// <param name="profileName"></param>
    /// <returns></returns>
    public static FolderTree Empty(string profileName) => Build(profileName, [], [], CancellationToken.None);

    /// <summary>
    /// 項目からﾂﾘーを作る
    /// </summary>
    /// <param name="profileName">ﾌﾟﾛﾌｧｲﾙ名</param>
    /// <param name="searchPaths">走査ﾙーﾄ</param>
    /// <param name="items">全件</param>
    /// <param name="token"></param>
    /// <returns></returns>
    public static FolderTree Build(string profileName, IReadOnlyList<string> searchPaths, IReadOnlyList<FileItem> items, CancellationToken token)
    {
        // ﾌｫﾙﾀﾞーごとの件数
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var i = 0; i < items.Count; i++) {
            if ((i & 0xFFF) == 0) {
                token.ThrowIfCancellationRequested();
            }
            CollectionsMarshal.GetValueRefOrAddDefault(counts, items[i].Path, out _)++;
        }

        var root = new FolderNode(profileName, null, null);
        var locationMap = new Dictionary<string, FolderNode>(StringComparer.Ordinal);
        var registry = FileSourceRegistry.Instance;
        foreach (var folder in counts.Keys) {
            token.ThrowIfCancellationRequested();
            if (string.IsNullOrEmpty(folder)) {
                continue;
            }
            if (registry.TryResolve(folder, out var source) && FindTop(folder, source, searchPaths) is { } top) {
                Gatalyze(folder, source, top, root, locationMap);
            }
            else if (!locationMap.ContainsKey(folder)) {
                // どの走査ﾙーﾄにも属さないのはﾌﾙﾊﾟｽのまま最上位直下に置く
                var loose = new FolderNode(folder, folder, root);
                root.children.Add(loose);
                locationMap.Add(folder, loose);
            }
        }

        // 名前順に並べて先行順で番号を振る
        var ordered = new List<FolderNode>(locationMap.Count + 1);
        var parentOrders = new List<int>(locationMap.Count + 1);
        Numero(root, -1, ordered, parentOrders);

        // 配下の全件数
        var totals = new int[ordered.Count];
        for (var i = 0; i < ordered.Count; i++) {
            if (ordered[i].Location is { } location && counts.TryGetValue(location, out var count)) {
                totals[i] = count;
            }
        }
        for (var i = totals.Length - 1; i > 0; i--) {
            totals[parentOrders[i]] += totals[i];
        }
        for (var i = 0; i < ordered.Count; i++) {
            ordered[i].TotalCount = totals[i];
            ordered[i].HitCount = totals[i];
        }

        // 最上位と、走査ﾙーﾄが1個ならそれも開いておく
        root.IsExpanded = true;
        if (root.children.Count == 1) {
            root.children[0].IsExpanded = true;
        }
        return new FolderTree(root, ordered.ToArray(), parentOrders.ToArray(), locationMap);
    }

    /// <summary>
    /// ﾌｫﾙﾀﾞーが属する走査ﾙーﾄ(入れ子なら一番深いもの、無ければnull)
    /// </summary>
    /// <param name="folder"></param>
    /// <param name="source"></param>
    /// <param name="searchPaths"></param>
    /// <returns></returns>
    private static string? FindTop(string folder, IFileSource source, IReadOnlyList<string> searchPaths)
    {
        string? top = null;
        foreach (var path in searchPaths) {
            try {
                if (!FileSourceRegistry.Instance.TryResolve(path, out var rootSource) || !ReferenceEquals(rootSource, source)) {
                    continue;
                }
                if (source.IsSubdirectoryOrSame(folder, path) && (top == null || path.Length > top.Length)) {
                    top = path;
                }
            }
            catch {
                // 変な場所は配下扱いしない
            }
        }
        return top;
    }

    /// <summary>
    /// 場所の節を(親も含めて)作る
    /// </summary>
    /// <param name="location"></param>
    /// <param name="source"></param>
    /// <param name="top">走査ﾙーﾄ</param>
    /// <param name="root">最上位</param>
    /// <param name="locationMap"></param>
    /// <returns></returns>
    private static FolderNode Gatalyze(string location, IFileSource source, string top, FolderNode root, Dictionary<string, FolderNode> locationMap)
    {
        if (locationMap.TryGetValue(location, out var node)) {
            return node;
        }
        FolderNode parentNode;
        string name;
        var parent = IsSameOrAbove(location, top, source) ? null : source.GetParent(location);
        if (parent == null) {
            // 走査ﾙーﾄに着いた
            parentNode = root;
            name = location;
        }
        else {
            parentNode = Gatalyze(parent, source, top, root, locationMap);
            name = location.StartsWith(parent, StringComparison.Ordinal)
                ? location[parent.Length..].Trim('\\', '/')
                : location;
            if (name.Length == 0) {
                name = location;
            }
        }
        node = new FolderNode(name, location, parentNode);
        parentNode.children.Add(node);
        locationMap.Add(location, node);
        return node;
    }

    /// <summary>
    /// locationは走査ﾙーﾄかそれより上？
    /// </summary>
    /// <param name="location"></param>
    /// <param name="top"></param>
    /// <param name="source"></param>
    /// <returns></returns>
    private static bool IsSameOrAbove(string location, string top, IFileSource source)
    {
        try {
            return source.IsSubdirectoryOrSame(top, location);
        }
        catch {
            return true;
        }
    }

    /// <summary>
    /// 子を名前順に並べて先行順で番号を振る
    /// </summary>
    /// <param name="root"></param>
    /// <param name="rootParent"></param>
    /// <param name="ordered"></param>
    /// <param name="parentOrders"></param>
    private static void Numero(FolderNode root, int rootParent, List<FolderNode> ordered, List<int> parentOrders)
    {
        // 深いﾌｫﾙﾀﾞーで再帰が溢れないよう自前のｽﾀｯｸで回す
        var stack = new Stack<(FolderNode Node, int Parent, bool Exit)>();
        stack.Push((root, rootParent, false));
        while (stack.Count != 0) {
            var (node, parent, exit) = stack.Pop();
            if (exit) {
                node.End = ordered.Count;
                continue;
            }
            node.Order = ordered.Count;
            ordered.Add(node);
            parentOrders.Add(parent);
            node.children.Sort(static (a, b) => StringComparer.CurrentCultureIgnoreCase.Compare(a.Name, b.Name));
            stack.Push((node, parent, true));
            for (var i = node.children.Count - 1; i >= 0; i--) {
                stack.Push((node.children[i], node.Order, false));
            }
        }
    }
}

/// <summary>
/// ﾌｫﾙﾀﾞーとそのｻﾌﾞﾌｫﾙﾀﾞーで絞り込む条件
/// </summary>
public sealed class FolderScope
{
    readonly FolderTree tree;
    readonly int start;
    readonly int end;

    internal FolderScope(FolderTree folderTree, FolderNode node)
    {
        tree = folderTree;
        Node = node;
        start = node.Order;
        end = node.End;
    }

    /// <summary>
    /// 選択中の節
    /// </summary>
    public FolderNode Node { get; }

    /// <summary>
    /// 項目は配下に入ってる？
    /// </summary>
    /// <param name="item"></param>
    /// <returns></returns>
    public bool Contains(FileItem item)
    {
        var order = tree.OrderOf(item);
        return order >= start && order < end;
    }
}
