using ImogeIndex.Mur;
using ImogeIndex.Web6.Modelle;

namespace ImogeIndex.Web6.Committees;

/// <summary>
/// 左ﾍﾟｲﾝのﾌｫﾙﾀﾞーﾂﾘーを統治する
/// 最上位選択中は検索結果でﾂﾘーも絞り、それ以外のﾌｫﾙﾀﾞー選択中は一覧だけをそのﾌｫﾙﾀﾞー配下で絞る
/// </summary>
public sealed class FolderTreeCommittee
{
    readonly ImooneModell model;
    readonly FileListSearchCommittee search;

    // 件数の数え直しが追い越されたら捨てる用
    int hitVersion;

    /// <summary>
    /// ﾀー
    /// </summary>
    /// <param name="modell">ﾓﾃﾞﾚ</param>
    /// <param name="fileListSearch">一覧の検索</param>
    public FolderTreeCommittee(ImooneModell modell, FileListSearchCommittee fileListSearch)
    {
        model = modell;
        search = fileListSearch;
        search.ListPublished += OnListPublished;
    }

    /// <summary>
    /// 現在のﾂﾘー(読み込み前はnull)
    /// </summary>
    public FolderTree? Tree { get; private set; }

    /// <summary>
    /// 選択中の節
    /// </summary>
    public FolderNode? SelectedNode { get; private set; }

    /// <summary>
    /// 選択中のﾌｫﾙﾀﾞーの場所(最上位なら空文字)
    /// </summary>
    public string SelectedLocation => SelectedNode?.Location ?? string.Empty;

    /// <summary>
    /// ﾂﾘーを作り直して、前回の選択を復元する
    /// 一覧は作り直さないので、この後でFileListSearchCommittee.ReloadAsyncを呼ぶこと
    /// </summary>
    /// <param name="profileName">ﾌﾟﾛﾌｧｲﾙ名</param>
    /// <param name="searchPaths">走査ﾙーﾄ</param>
    /// <param name="items">全件</param>
    /// <param name="selectLocation">選択するﾌｫﾙﾀﾞー(無い/空なら最上位)</param>
    /// <param name="token"></param>
    /// <returns></returns>
    public async Task LoadAsync(string profileName, IReadOnlyList<string> searchPaths, IReadOnlyList<FileItem> items,
        string? selectLocation, CancellationToken token)
    {
        hitVersion++;
        var tree = await Task.Run(() => FolderTree.Build(profileName, searchPaths, items, token), token);
        Tree = tree;
        var node = tree.Find(selectLocation) ?? tree.Root;
        SelectedNode = node;
        search.SetFolderScope(tree.SanitizeScope(node), false);
        model.FolderTreeRoots.Clear();
        model.FolderTreeRoots.Add(tree.Root);
        FolderTree.Reveal(node);
    }

    /// <summary>
    /// 節が選択された
    /// </summary>
    /// <param name="node"></param>
    /// <returns>選択が変わった？</returns>
    public bool Select(FolderNode? node)
    {
        if (Tree is not { } tree || node == null || ReferenceEquals(node, SelectedNode)) {
            return false;
        }
        var scope = tree.SanitizeScope(node);
        if (scope == null && !ReferenceEquals(node, tree.Root)) {
            // 古いﾂﾘーの節
            return false;
        }
        SelectedNode = node;
        search.SetFolderScope(scope);
        return true;
    }

    /// <summary>
    /// 一覧が差し替わったので件数を数え直す
    /// </summary>
    /// <param name="items"></param>
    /// <param name="treeApplicable"></param>
    private async void OnListPublished(IReadOnlyList<FileItem> items, bool treeApplicable)
    {
        var ver = ++hitVersion;
        if (!treeApplicable || Tree is not { } tree) {
            // ﾌｫﾙﾀﾞー選択中や類似検索中はﾂﾘーを固定
            return;
        }
        int[] hits;
        try {
            hits = await Task.Run(() => tree.ComputeHits(items, CancellationToken.None));
        }
        catch (Exception) {
            // 数えられなくても一覧には響かないのでｼｶﾄ
            return;
        }
        if (ver != hitVersion || !ReferenceEquals(tree, Tree)) {
            return;
        }
        tree.ApplyHits(hits);
    }
}
