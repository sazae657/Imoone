using ImogeIndex.Common;
using ImogeIndex.Mur;
using ImooneWPF.Modelle;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace ImooneWPF.Committees;

// TODO: 後から来た要求が前の要求を追い越せるよう、要求ﾊﾞーｼﾞｮﾝとCTSと待ち合わせ用ﾀｽｸの三点で競合防御してるが
// TODO: もうちょいｽﾏーﾄなが方法ないか考える

/// <summary>
/// 検索(通常検索/類似検索)と一覧の差し替えを統治する
/// </summary>
internal sealed class FileListSearchCommittee
{
    private const int SearchDelayMilliseconds = 300;

    readonly ListView list;
    readonly TextBox searchBox;
    readonly MainWindowModell model;
    readonly FileListColumnCommittee columns;
    readonly ImogeDatum datum;
    readonly Action clearDetailDelegaty;
    readonly Action<Exception> showErrorDelegaty;
    readonly CancellationToken closingToken;

    readonly MurSearcher fileSearcher = new();
    readonly FileListSourceCommittee fileListSource = new();
    readonly DispatcherTimer searchTimer = new()
    {
        Interval = TimeSpan.FromMilliseconds(SearchDelayMilliseconds)
    };

    CancellationTokenSource? searchTokenSource;
    Task pendingSearch = Task.CompletedTask;
    long searchRequestVersion;
    FileItem? similarSearchTarget;

    /// <summary>
    /// </summary>
    /// <param name="fileList">結果を流す一覧</param>
    /// <param name="searchTextBox">検索ﾎﾞｯｸｽ</param>
    /// <param name="modell">ﾓﾃﾞﾚ</param>
    /// <param name="columnCommittee">並び順と並べ替え表示</param>
    /// <param name="imogeDatum">検索対象のﾃﾞーﾀﾑ</param>
    /// <param name="clearDetail">詳細ﾍﾟｲﾝを空にする</param>
    /// <param name="showError">ｴﾗー表示</param>
    /// <param name="closing">ｳｨﾝﾄﾞｳが閉じられたらｷｬﾝｾﾙされるﾄーｸﾝ</param>
    public FileListSearchCommittee(ListView fileList, TextBox searchTextBox, MainWindowModell modell,
        FileListColumnCommittee columnCommittee, ImogeDatum imogeDatum,
        Action clearDetail, Action<Exception> showError, CancellationToken closing)
    {
        list = fileList;
        searchBox = searchTextBox;
        model = modell;
        columns = columnCommittee;
        datum = imogeDatum;
        clearDetailDelegaty = clearDetail;
        showErrorDelegaty = showError;
        closingToken = closing;

        searchTimer.Tick += SearchTimer_Tick;
        searchBox.TextChanged += SearchBox_TextChanged;
        searchBox.KeyDown += SearchBox_KeyDown;
    }

    /// <summary>
    /// 一覧の差し替え中？
    /// 差し替えに伴う選択の付け外しを利用側で無視するため
    /// </summary>
    public bool IsSwappingItemsSource { get; private set; }

    /// <summary>
    /// 読み込み順の全件
    /// </summary>
    public IReadOnlyList<FileItem> Items => fileListSource.Items;

    /// <summary>
    /// 類似検索中の基準画像(類似検索中でなければnull)
    /// </summary>
    public FileItem? SimilarTarget => model.IsSimilarSearch ? similarSearchTarget : null;

    /// <summary>
    /// 並べ替えできる状態？
    /// 類似検索は距離順で並べているので並べ替えさせない
    /// </summary>
    public bool CanResort => !model.IsSimilarSearch && list.ItemsSource != null;

    bool IsClosing => closingToken.IsCancellationRequested;

    #region 入力

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        searchTimer.Stop();
        if (model.IsSimilarSearch) {
            // 類似検索は解除して元の一覧に戻し、検索自体は通常の流れに任せる
            Track(RestoreNormalListAsync());
        }
        else if (model.IncrementalSearch) {
            // Enter確定ﾓーﾄﾞでは入力だけで実行中の検索を止めない
            CancelSearchRequest();
        }
        if (model.IncrementalSearch) {
            searchTimer.Start();
        }
    }

    private void SearchBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (!model.IncrementalSearch && e.Key == Key.Enter) {
            Search();
            e.Handled = true;
        }
    }

    private void SearchTimer_Tick(object? sender, EventArgs e)
    {
        searchTimer.Stop();
        if (model.IncrementalSearch) {
            Track(ApplySearchAsync());
        }
    }

    #endregion

    #region 操作用

    /// <summary>
    /// 検索ﾎﾞｯｸｽの内容で検索実行
    /// </summary>
    public void Search()
    {
        searchTimer.Stop();
        Track(ApplySearchAsync());
    }

    /// <summary>
    /// 類似画像検索
    /// </summary>
    /// <param name="target"></param>
    public void SearchSimilar(FileItem target)
    {
        if (IsClosing || !target.HasImageHashes) {
            return;
        }
        Track(ApplySimilarSearchAsync(target));
    }

    /// <summary>
    /// 走っている検索を止めて終わるまで待ち、類似検索ﾓーﾄﾞも抜ける
    /// </summary>
    public async Task QuiesceAsync()
    {
        searchTimer.Stop();
        CancelSearchRequest();
        try {
            await pendingSearch;
        }
        catch {
            // 意図的に打ち切ったのでｼｶﾄ
        }
        pendingSearch = Task.CompletedTask;
        ClearSimilarSearchMode();
    }

    /// <summary>
    /// 全件を差し替えて一覧を作り直し、検索条件を復元する
    /// </summary>
    /// <param name="items"></param>
    /// <returns></returns>
    public async Task ReloadAsync(IReadOnlyList<FileItem> items)
    {
        fileSearcher.Reset();
        fileListSource.Items = items;
        fileListSource.Filter = null;
        fileListSource.Comparer = columns.CreateComparer();
        model.FileCountText = Properties.Resources.MC_NItems.Morfat(fileListSource.Items.Count);
        await PublishFileListAsync(closingToken);
        if (!IsClosing) {
            // 検索条件を復元(たぶん上手く行く)
            var task = ApplySearchAsync();
            pendingSearch = task;
            await task;
        }
    }

    /// <summary>
    /// 現在の並び順で一覧を作り直す
    /// </summary>
    /// <returns></returns>
    public async Task ResortAsync()
    {
        fileListSource.Comparer = columns.CreateComparer();
        try {
            await PublishFileListAsync(closingToken);
        }
        catch (OperationCanceledException) {
            // 閉じられた
        }
    }

    /// <summary>
    /// 閉じる時に走っている検索を止める
    /// </summary>
    public void Stop()
    {
        searchTimer.Stop();
        searchTokenSource?.Cancel();
    }

    #endregion

    #region 検索本体

    /// <summary>
    /// 待ち合わせ対象として覚えた上で投げっぱにする
    /// </summary>
    /// <param name="task"></param>
    private async void Track(Task task)
    {
        pendingSearch = task;
        await task;
    }

    /// <summary>
    /// 検索中止
    /// </summary>
    private void CancelSearchRequest()
    {
        searchRequestVersion++;
        searchTokenSource?.Cancel();
        fileListSource.Invalidate();
    }

    /// <summary>
    /// 検索実行
    /// ﾌﾘーｽﾞすると気持ち悪いので判定はｽﾚﾌﾟで、結果のIDだけをﾌｨﾙﾀーに渡す
    /// </summary>
    private async Task ApplySearchAsync()
    {
        if (list.ItemsSource == null) {
            return;
        }

        // 走っている検索は用済みなので止める
        CancelSearchRequest();
        ClearSimilarSearchMode();
        var request = searchRequestVersion;
        var cts = CancellationTokenSource.CreateLinkedTokenSource(closingToken);
        searchTokenSource = cts;

        try {
            var query = new MurQuery();
            query.Parse(searchBox.Text);

            if (query.IsEmpty()) {
                // 検索文字が空ならﾌｨﾙﾀー解除
                fileSearcher.Reset();
                fileListSource.Filter = null;
            }
            else {
                var result = await fileSearcher.SearchAsync(query, datum, cts.Token);
                if (cts.Token.IsCancellationRequested || request != searchRequestVersion) {
                    return;
                }
                fileListSource.Filter = result.Ids;
            }
            await PublishFileListAsync(cts.Token);
        }
        catch (OperationCanceledException) {
            // 新しい入力に置き換わっただけ
        }
        finally {
            if (ReferenceEquals(searchTokenSource, cts)) {
                searchTokenSource = null;
            }
            cts.Dispose();
        }
    }

    /// <summary>
    /// 現在の検索結果とｿーﾄ状態で一覧を作り直して差し替える
    /// ｿーﾄと絞り込みはｽﾚﾌﾟで済ませて、UIｽﾚｯﾄﾞではItemsSourceを差し替えるだけにする
    /// </summary>
    /// <param name="token"></param>
    /// <returns></returns>
    private async Task PublishFileListAsync(CancellationToken token)
    {
        var built = await fileListSource.BuildAsync(token);
        if (built == null || IsClosing || token.IsCancellationRequested) {
            // 後続の要求に追い越された
            return;
        }

        var selected = list.SelectedItem as FileItem;
        IsSwappingItemsSource = true;
        try {
            list.ItemsSource = built;
            if (selected != null && fileListSource.IsVisible(selected)) {
                list.SelectedItem = selected;
                list.ScrollIntoView(selected);
            }
        }
        finally {
            IsSwappingItemsSource = false;
        }
        model.FileCountText = Properties.Resources.MC_NItems.Morfat(built.Count);
    }

    /// <summary>
    /// 類似検索ﾓーﾄﾞを抜ける
    /// </summary>
    private void ClearSimilarSearchMode()
    {
        if (model.IsSimilarSearch) {
            columns.SetSortIndicatorVisible(true);
        }
        fileListSource.RankedItems = null;
        similarSearchTarget = null;
        model.IsSimilarSearch = false;
        model.SimilarSearchText = string.Empty;
    }

    /// <summary>
    /// 直近の通常検索状態を復元
    /// </summary>
    /// <returns></returns>
    private async Task RestoreNormalListAsync()
    {
        CancelSearchRequest();
        ClearSimilarSearchMode();
        var cts = CancellationTokenSource.CreateLinkedTokenSource(closingToken);
        searchTokenSource = cts;
        try {
            await PublishFileListAsync(cts.Token);
        }
        catch (OperationCanceledException) {
            // 後続の検索に置き換わった
        }
        finally {
            if (ReferenceEquals(searchTokenSource, cts)) {
                searchTokenSource = null;
            }
            cts.Dispose();
        }
    }

    /// <summary>
    /// 類似画像検索本体
    /// </summary>
    /// <param name="target"></param>
    /// <returns></returns>
    private async Task ApplySimilarSearchAsync(FileItem target)
    {
        searchTimer.Stop();
        CancelSearchRequest();
        var request = searchRequestVersion;
        var source = fileListSource.Items;
        var mosh = model.SimilarHashes;
        var cts = CancellationTokenSource.CreateLinkedTokenSource(closingToken);
        searchTokenSource = cts;
        model.IsSimilarSearch = true;
        similarSearchTarget = target;
        columns.SetSortIndicatorVisible(false);
        model.SimilarSearchText = Properties.Resources.MC_SimilarSearchActive.Morfat(target.Name, SimilarImageSearcher.ResultLimit);
        try {
            fileListSource.RankedItems = Array.Empty<FileItem>();
            clearDetailDelegaty();
            await PublishFileListAsync(cts.Token);
            model.FileCountText = Properties.Resources.MC_SimilarSearching;
            var matches = await SimilarImageSearcher.SearchMoone(target, source, mosh, cts.Token);
            if (cts.IsCancellationRequested || request != searchRequestVersion
                || !ReferenceEquals(source, fileListSource.Items) || IsClosing) {
                return;
            }
            // 比較元が見えないとﾀﾞｻいので先頭に固定して、類似画像はその後に距離順で並べる
            fileListSource.RankedItems = matches.Select(x => x.Item).Prepend(target).ToArray();
            await PublishFileListAsync(cts.Token);
            if (cts.IsCancellationRequested || request != searchRequestVersion || IsClosing) {
                return;
            }
            list.SelectedItem = target;
            list.ScrollIntoView(target);
        }
        catch (OperationCanceledException) {
            // 後続の検索やﾌﾟﾛﾌｧｲﾙ切り替えに置き換わった。
        }
        catch (Exception ex) {
            if (request == searchRequestVersion && !IsClosing) {
                showErrorDelegaty(ex);
                ClearSimilarSearchMode();
                await ApplySearchAsync();
            }
        }
        finally {
            if (ReferenceEquals(searchTokenSource, cts)) {
                searchTokenSource = null;
            }
            cts.Dispose();
        }
    }

    #endregion
}
