namespace ImogeIndex.Zenerators;

public delegate void StateDelegaty();
public delegate void MessageDelegaty(string message);
public delegate void ErrorDelegaty(Exception e, string message);
public delegate void ProgressDelegaty(int c, int total, int error, string phase, string message);
public delegate void SubProgressDelegaty(int index, double progress);

public interface IProzessor : IDisposable {
    /// <summary>
    /// 実行中？
    /// </summary>
    bool Running { get; }

    /// <summary>
    /// 実行中の処理の終了まち
    /// </summary>
    Task WaitFor();

    /// <summary>
    /// すんごい長い処理が始まる
    /// </summary>
    event StateDelegaty? BeginVeryLongProcess;

    /// <summary>
    /// すんごい長い処理が終わった
    /// </summary>
    event StateDelegaty? EndVeryLongProcess;
}

public interface IIndexingResult {
    bool Success { get; set; }
    Exception? Exception { get; set; }

    /// <summary>
    /// 停止要求で中断した(失敗ではない)
    /// </summary>
    bool Cancelled { get; set; }
    bool WasFailed();
}

/// <summary>
/// ｶｳﾝﾀー
/// </summary>
public class Collective {
    int removed;
    public void 削() { Interlocked.Increment(ref removed); }
    public void 削(int c) { Interlocked.Add(ref removed, c); }

    int added;
    public void 追() { Interlocked.Increment(ref added); }
    public void 追(int c) { Interlocked.Add(ref added, c); }

    int modified;
    public void 更() { Interlocked.Increment(ref modified); }
    public void 更(int c) { Interlocked.Add(ref modified, c); }

    public int Removed => removed;
    public int Added => added;

    public int Modified => modified;

    public bool WasModified => removed != 0 || added != 0 || modified != 0;

    public void Clear() {
        removed = 0;
        added = 0;
        modified = 0;
    }
}
