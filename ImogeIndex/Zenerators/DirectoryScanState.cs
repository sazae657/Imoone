using ImogeIndex.Sources;

namespace ImogeIndex.Zenerators;

/// <summary>
/// ﾌｫﾙﾀﾞーｽｷｬﾝ状態
/// </summary>
internal sealed class DirectoryScanState {
    // 無効日付
    internal const long NeedsRescan = long.MinValue;
    readonly FileSourceRegistry sources;
    readonly HashSet<string> deferred;
    readonly Dictionary<string, bool> protection;
    // 中身は列挙できたが、何らかの事情で一部のﾌｧｲﾙが読めなかったﾌｫﾙﾀﾞー
    readonly HashSet<string> retry;

    public DirectoryScanState(FileSourceRegistry? sources = null) {
        this.sources = sources ?? FileSourceRegistry.Instance;
        deferred = new(this.sources.Comparer);
        protection = new(this.sources.Comparer);
        retry = new(this.sources.Comparer);
    }

    public int Count => deferred.Count;

    /// <summary>
    /// 読めなかったﾌｧｲﾙの数
    /// </summary>
    public int FailedFiles { get; private set; }

    /// <summary>
    /// ﾘﾄﾗｲ
    /// </summary>
    /// <param name="path"></param>
    /// <param name="failedFiles"></param>
    public void Retry(string path, int failedFiles) {
        // ﾌｧｲﾙ単位の失敗なので、そのﾌｫﾙﾀﾞーだけ次回もう一回列挙させる
        retry.Add(Normalize(path));
        FailedFiles += failedFiles;
    }

    /// <summary>
    /// 次回も列挙し直す必要がある？
    /// </summary>
    /// <param name="path"></param>
    /// <returns></returns>
    public bool ShouldRescan(string path) => retry.Contains(Normalize(path)) || Contains(path);

    /// <summary>
    /// あとでやろうはｸｿ野郎だけどI/Oｴﾗーなので仕方ない
    /// </summary>
    /// <param name="path"></param>
    /// <returns></returns>
    public bool Defer(string path) {
        protection.Clear();
        return deferred.Add(Normalize(path));
    }

    /// <summary>
    /// そいつは後回し？
    /// </summary>
    /// <param name="path"></param>
    /// <returns></returns>
    public bool Contains(string path) {
        if (!sources.TryResolve(path, out var source)) {
            // 未登録のｽｷーﾑは親を辿れないのでそのものだけ見る
            return deferred.Contains(path);
        }
        path = source.Normalize(path);
        if (protection.TryGetValue(path, out var result)) {
            return result;
        }
        var current = path;
        while (current != null) {
            if (deferred.Contains(current)) {
                protection[path] = true;
                return true;
            }
            current = source.GetParent(current);
        }
        protection[path] = false;
        return false;
    }

    // 未登録のｽｷーﾑはそのまま(それ自体を後回しにするために呼ばれる)
    string Normalize(string path) => sources.TryResolve(path, out var source) ? source.Normalize(path) : path;
}
