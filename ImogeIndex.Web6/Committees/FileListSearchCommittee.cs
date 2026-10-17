using ImogeIndex.Web6.Modelle;
using ImogeIndex.Mur;

namespace ImogeIndex.Web6.Committees;

/// <summary>
/// 検索結果を流す一覧(UIﾌﾚーﾑﾜーｸ側で実装する)
/// </summary>
public interface IFileListHost
{
    /// <summary>
    /// 検索ﾎﾞｯｸｽの文字列
    /// </summary>
    string QueryText { get; }

    /// <summary>
    /// 一覧に何か流してある？
    /// </summary>
    bool HasItemsSource { get; }

    /// <summary>
    /// 選択中の項目
    /// </summary>
    FileItem? SelectedItem { get; }

    /// <summary>
    /// 一覧を差し替える
    /// </summary>
    /// <param name="items"></param>
    void Publish(IReadOnlyList<FileItem> items);

    /// <summary>
    /// 選択して見える所までｽｸﾛーﾙ
    /// </summary>
    /// <param name="item"></param>
    void Reveal(FileItem item);

    /// <summary>
    /// 現在の並び順の比較子(ｿーﾄしないならnull)
    /// </summary>
    /// <returns></returns>
    IComparer<FileItem>? CreateComparer();

    /// <summary>
    /// ｿーﾄ矢印の表示切り替え(類似検索中は消す)
    /// </summary>
    /// <param name="visible"></param>
    void SetSortIndicatorVisible(bool visible);
}

// TODO: 後から来た要求が前の要求を追い越せるよう、要求ﾊﾞーｼﾞｮﾝとCTSと待ち合わせ用ﾀｽｸの三点で競合防御してるが
// TODO: もうちょいｽﾏーﾄなが方法ないか考える

/// <summary>
/// 検索(通常検索/類似検索)と一覧の差し替えを統治する
/// </summary>
public sealed class FileListSearchCommittee
{
    private const int SearchDelayMilliseconds = 300;

    readonly IFileListHost list;
    readonly ImooneModell model;
    readonly IImoonePalabras texts;
    readonly ImogeDatum datum;
    readonly Action clearDetailDelegaty;
    readonly Action<Exception> showErrorDelegaty;
    readonly CancellationToken closingToken;

    readonly MurSearcher fileSearcher = new();
    readonly FileListSourceCommittee fileListSource = new();

    // ｲﾝｸﾘﾒﾝﾀﾙ検索の入力待ち
    CancellationTokenSource? searchDelaySource;
    CancellationTokenSource? searchTokenSource;
    Task pendingSearch = Task.CompletedTask;
    long searchRequestVersion;
    FileItem? similarSearchTarget;

    /// <summary>
    /// ﾀー
    /// </summary>
    /// <param name="fileList">結果を流す一覧</param>
    /// <param name="modell">ﾓﾃﾞﾚ</param>
    /// <param name="imooneTexts">表示文言</param>
    /// <param name="imogeDatum">検索対象のﾃﾞーﾀﾑ</param>
    /// <param name="clearDetail">詳細ﾍﾟｲﾝを空にする</param>
    /// <param name="showError">ｴﾗー表示</param>
    /// <param name="closing">ｳｨﾝﾄﾞｳが閉じられたらｷｬﾝｾﾙされるﾄーｸﾝ</param>
    public FileListSearchCommittee(IFileListHost fileList, ImooneModell modell, IImoonePalabras imooneTexts,
        ImogeDatum imogeDatum, Action clearDetail, Action<Exception> showError, CancellationToken closing)
    {
        list = fileList;
        model = modell;
        texts = imooneTexts;
        datum = imogeDatum;
        clearDetailDelegaty = clearDetail;
        showErrorDelegaty = showError;
        closingToken = closing;
    }

    /// <summary>
    /// 一覧の差し替え中？
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
    public bool CanResort => !model.IsSimilarSearch && list.HasItemsSource;

    bool IsClosing => closingToken.IsCancellationRequested;

    #region 入力

    /// <summary>
    /// 検索ﾎﾞｯｸｽの文字列が変わった
    /// </summary>
    public void NotifyQueryChanged()
    {
        StopSearchDelay();
        if (model.IsSimilarSearch) {
            // 類似検索は解除して元の一覧に戻し、検索自体は通常の流れに任せる
            Track(RestoreNormalListAsync());
        }
        else if (model.IncrementalSearch) {
            // Enter確定ﾓーﾄﾞでは入力だけで実行中の検索を止めない
            CancelSearchRequest();
        }
        if (model.IncrementalSearch) {
            StartSearchDelay();
        }
    }

    /// <summary>
    /// 検索ﾎﾞｯｸｽでEnterが押された
    /// </summary>
    /// <returns>処理した？</returns>
    public bool NotifyEnter()
    {
        if (model.IncrementalSearch) {
            return false;
        }
        Search();
        return true;
    }

    /// <summary>
    /// 入力が落ち着くまで待ってから検索する
    /// </summary>
    private async void StartSearchDelay()
    {
        var cts = new CancellationTokenSource();
        searchDelaySource = cts;
        try {
            await Task.Delay(SearchDelayMilliseconds, cts.Token);
        }
        catch (OperationCanceledException) {
            // 次の入力が来た
            return;
        }
        if (!ReferenceEquals(searchDelaySource, cts)) {
            return;
        }
        StopSearchDelay();
        if (model.IncrementalSearch) {
            Track(ApplySearchAsync());
        }
    }

    /// <summary>
    /// 入力待ちを止める
    /// </summary>
    private void StopSearchDelay()
    {
        searchDelaySource?.Cancel();
        searchDelaySource?.Dispose();
        searchDelaySource = null;
    }

    #endregion

    #region 操作用

    /// <summary>
    /// 検索ﾎﾞｯｸｽの内容で検索実行
    /// </summary>
    public void Search()
    {
        StopSearchDelay();
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
        StopSearchDelay();
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
        fileListSource.Comparer = list.CreateComparer();
        model.FileCountText = texts.ItemCount(fileListSource.Items.Count);
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
        fileListSource.Comparer = list.CreateComparer();
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
        StopSearchDelay();
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
        if (!list.HasItemsSource) {
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
            query.Parse(list.QueryText);

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

        var selected = list.SelectedItem;
        IsSwappingItemsSource = true;
        try {
            list.Publish(built);
            if (selected != null && fileListSource.IsVisible(selected)) {
                list.Reveal(selected);
            }
        }
        finally {
            IsSwappingItemsSource = false;
        }
        model.FileCountText = texts.ItemCount(built.Count);
    }

    /// <summary>
    /// 類似検索ﾓーﾄﾞを抜ける
    /// </summary>
    private void ClearSimilarSearchMode()
    {
        if (model.IsSimilarSearch) {
            list.SetSortIndicatorVisible(true);
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
        StopSearchDelay();
        CancelSearchRequest();
        var request = searchRequestVersion;
        var source = fileListSource.Items;
        var mosh = model.SimilarHashes;
        var cts = CancellationTokenSource.CreateLinkedTokenSource(closingToken);
        searchTokenSource = cts;
        model.IsSimilarSearch = true;
        similarSearchTarget = target;
        list.SetSortIndicatorVisible(false);
        model.SimilarSearchText = texts.SimilarSearchActive(target.Name, SimilarImageSearcher.ResultLimit);
        try {
            fileListSource.RankedItems = Array.Empty<FileItem>();
            clearDetailDelegaty();
            await PublishFileListAsync(cts.Token);
            model.FileCountText = texts.SimilarSearching;
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
            list.Reveal(target);
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
