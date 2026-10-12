namespace ImogeIndex.Zenerators;

/// <summary>
/// ﾌﾟﾛｾｯｻーの実行状態管理
/// </summary>
internal sealed class ExecutionStateObzekt {
    readonly object sync = new();
    Task completion = Task.CompletedTask;

    /// <summary>
    /// 実行中？
    /// </summary>
    public bool Running {
        get {
            lock (sync) {
                return !completion.IsCompleted;
            }
        }
    }

    /// <summary>
    /// 実行中状態にする
    /// </summary>
    /// <returns>こいつをDisposeすると完了状態</returns>
    public IDisposable? TryStart() {
        lock (sync) {
            if (!completion.IsCompleted) {
                return null;
            }
            var execution = new Exe();
            completion = execution.Completion;
            return execution;
        }
    }

    /// <summary>
    /// 完了待ち
    /// </summary>
    /// <returns></returns>
    public Task WaitFor() {
        lock (sync) {
            return completion;
        }
    }

    /// <summary>
    /// 実行状態
    /// </summary>
    sealed class Exe : IDisposable {
        readonly TaskCompletionSource completion = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>
        /// 完了状態
        /// </summary>
        public Task Completion => completion.Task;

        /// <summary>
        /// Disposeすると完了になる
        /// </summary>
        public void Dispose() => completion.TrySetResult();
    }
}
