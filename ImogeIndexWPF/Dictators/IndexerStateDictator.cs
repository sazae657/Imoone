namespace ImogeIndexWPF.Dictators;

/// <summary>
/// 現在の状況
/// </summary>
internal enum IndexerLifecycle
{
    /// <summary>
    /// 生成直後(Loaded前)
    /// </summary>
    Created,

    /// <summary>
    /// Loaded処理中<
    /// /summary>
    Initializing,

    /// <summary>
    /// 初期化完了、操作可能
    /// </summary>
    Ready,
    /// <summary>
    /// 終了処理中
    /// </summary>
    /// 
    Closing,
    /// <summary>
    /// 終了処理完了、閉じてよい
    /// </summary>
    Closed,
}

/// <summary>
/// 処理の実行段階
/// </summary>
internal enum IndexerPhase
{
    /// <summary>
    /// 何もしていない
    /// </summary>
    Idle,

    /// <summary>
    /// 開始直後、まだ停止できない
    /// </summary>
    Preparing,

    /// <summary>
    /// 実行中、停止できる
    /// </summary>
    /// 
    Running,
    /// <summary>
    /// 停止要求済み
    /// </summary>
    Stopping,
}

/// <summary>
/// 開始/停止/終了の状態管理
/// </summary>
internal sealed class IndexerStateDictator
{
    public IndexerLifecycle Lifecycle { get; private set; } = IndexerLifecycle.Created;

    public IndexerPhase Phase { get; private set; } = IndexerPhase.Idle;

    /// <summary>
    /// 状態が変わった
    /// </summary>
    public event Action? Changed;

    /// <summary>
    /// 終了処理中または終了済み
    /// </summary>
    public bool IsClosing => Lifecycle >= IndexerLifecycle.Closing;

    /// <summary>
    /// 終了処理が終わって閉じてよい
    /// </summary>
    public bool IsClosed => Lifecycle == IndexerLifecycle.Closed;

    /// <summary>
    /// 処理実行中(停止中を含む)
    /// </summary>
    public bool IsProcessing => Phase != IndexerPhase.Idle;

    /// <summary>
    /// 停止要求済み
    /// </summary>
    public bool IsStopping => Phase == IndexerPhase.Stopping;

    /// <summary>
    /// 処理を開始できる
    /// </summary>
    public bool CanStart => Lifecycle == IndexerLifecycle.Ready && Phase == IndexerPhase.Idle;

    /// <summary>
    /// 停止要求できる
    /// </summary>
    public bool CanStop => !IsClosing && Phase == IndexerPhase.Running;

    /// <summary>
    /// 初期化を開始する
    /// </summary>
    /// <returns>初回ならtrue</returns>
    public bool TryBeginInitialize()
    {
        if (Lifecycle != IndexerLifecycle.Created) {
            return false;
        }
        SetLifecycle(IndexerLifecycle.Initializing);
        return true;
    }

    /// <summary>
    /// 初期化完了
    /// </summary>
    public void CompleteInitialize()
    {
        if (Lifecycle != IndexerLifecycle.Initializing) {
            // 初期化中に終了処理が始まっていたら何もしない
            return;
        }
        SetLifecycle(IndexerLifecycle.Ready);
    }

    /// <summary>
    /// 処理を開始する
    /// </summary>
    /// <returns>開始できたらtrue</returns>
    public bool TryBeginProcess()
    {
        if (!CanStart) {
            return false;
        }
        SetPhase(IndexerPhase.Preparing);
        return true;
    }

    /// <summary>
    /// 停止できるようにする
    /// </summary>
    public void AllowStop()
    {
        if (Phase != IndexerPhase.Preparing) {
            return;
        }
        SetPhase(IndexerPhase.Running);
    }

    /// <summary>
    /// 停止要求
    /// </summary>
    /// <returns>停止要求を受け付けたらtrue</returns>
    public bool TryRequestStop()
    {
        if (!CanStop) {
            return false;
        }
        SetPhase(IndexerPhase.Stopping);
        return true;
    }

    /// <summary>
    /// 処理終了
    /// </summary>
    public void EndProcess()
    {
        SetPhase(IndexerPhase.Idle);
    }

    /// <summary>
    /// 終了処理を開始する
    /// </summary>
    /// <returns>初回ならtrue</returns>
    public bool TryBeginClose()
    {
        if (IsClosing) {
            return false;
        }
        SetLifecycle(IndexerLifecycle.Closing);
        return true;
    }

    /// <summary>
    /// 終了処理完了
    /// </summary>
    public void MarkClosed()
    {
        SetLifecycle(IndexerLifecycle.Closed);
    }

    void SetLifecycle(IndexerLifecycle value)
    {
        if (Lifecycle == value) {
            return;
        }
        Lifecycle = value;
        Changed?.Invoke();
    }

    void SetPhase(IndexerPhase value)
    {
        if (Phase == value) {
            return;
        }
        Phase = value;
        Changed?.Invoke();
    }
}
