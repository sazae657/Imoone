using ImogeIndex.Imoging;

namespace ImogeIndex.Mur;

/// <summary>
/// 類似検索に使うﾊｯｼｭの組み合わせ
/// </summary>
[Flags]
public enum SimilarHashAlgorithm
{
    None = 0,
    PHash = 1 << 0,
    AHash = 1 << 1,
    DHash = 1 << 2,
    WHash = 1 << 3,
    All = PHash | AHash | DHash | WHash,
}

/// <summary>
/// 基準画像からの距離(使わなかったﾊｯｼｭの距離は0)
/// </summary>
public readonly record struct SimilarImageMatch(
    FileItem Item, int Pstance, int Astance, int Dstance, int Wstance)
{
    // TODO: 問題が出たら重み調整する
    public int Distance => 2 * Pstance + Astance + Dstance + Wstance;
}

/// <summary>
/// 保存済みﾊｯｼｭから近い画像を最大ResultLimit件取得する
/// </summary>
public static class SimilarImageSearcher
{
    public const int ResultLimit = 100;

    /// <summary>
    /// 検索
    /// </summary>
    /// <param name="target"></param>
    /// <param name="source"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    public static Task<IReadOnlyList<SimilarImageMatch>> SearchMoone(
        FileItem target, IReadOnlyList<FileItem> source, CancellationToken token)
        => SearchMoone(target, source, SimilarHashAlgorithm.All, token);

    /// <summary>
    /// 使うﾊｯｼｭを指定して検索
    /// </summary>
    /// <param name="target"></param>
    /// <param name="source"></param>
    /// <param name="hashes"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    public static Task<IReadOnlyList<SimilarImageMatch>> SearchMoone(
        FileItem target, IReadOnlyList<FileItem> source, SimilarHashAlgorithm hashes, CancellationToken token)
        => Task.Run(() => Search(target, source, hashes, token), token);

    /// <summary>
    /// 検索
    /// </summary>
    /// <param name="target"></param>
    /// <param name="source"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    public static IReadOnlyList<SimilarImageMatch> Search(
        FileItem target, IReadOnlyList<FileItem> source, CancellationToken token)
        => Search(target, source, SimilarHashAlgorithm.All, token);

    /// <summary>
    /// 使うﾊｯｼｭを指定して検索
    /// </summary>
    /// <param name="target"></param>
    /// <param name="source"></param>
    /// <param name="hashes"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    public static IReadOnlyList<SimilarImageMatch> Search(
        FileItem target, IReadOnlyList<FileItem> source, SimilarHashAlgorithm hashes, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (!target.HasImageHashes) {
            throw new ArgumentException(Properties.Resources.MUR_E_HashNotCalculated, nameof(target));
        }
        if ((hashes & SimilarHashAlgorithm.All) == SimilarHashAlgorithm.None) {
            throw new ArgumentException(Properties.Resources.MUR_E_HashNotSelected, nameof(hashes));
        }
        var useP = hashes.HasFlag(SimilarHashAlgorithm.PHash);
        var useA = hashes.HasFlag(SimilarHashAlgorithm.AHash);
        var useD = hashes.HasFlag(SimilarHashAlgorithm.DHash);
        var useW = hashes.HasFlag(SimilarHashAlgorithm.WHash);

        // 最も遠い候補を先頭にして、全件をｿーﾄせずに上位だけ保持する。
        var cmp = Comparer<(int Distance, long Id)>.Create((a, b) => b.CompareTo(a));
        var best = new PriorityQueue<SimilarImageMatch, (int Distance, long Id)>(ResultLimit, cmp);
        for (var i = 0; i < source.Count; i++) {
            if ((i & 0x3FF) == 0) {
                token.ThrowIfCancellationRequested();
            }
            var item = source[i];
            if (item.Id == target.Id || !item.HasImageHashes) {
                continue;
            }
            var match = new SimilarImageMatch(item,
                useP ? ImageHasher.GetHammingDistance(target.PHash, item.PHash) : 0,
                useA ? ImageHasher.GetHammingDistance(target.AHash, item.AHash) : 0,
                useD ? ImageHasher.GetHammingDistance(target.DHash, item.DHash) : 0,
                useW ? ImageHasher.GetHammingDistance(target.WHash, item.WHash) : 0
            );
            var prt = (match.Distance, item.Id);
            if (best.Count < ResultLimit) {
                best.Enqueue(match, prt);
            }
            else {
                best.EnqueueDequeue(match, prt);
            }
        }
        token.ThrowIfCancellationRequested();
        var rc = new SimilarImageMatch[best.Count];
        for (var i = rc.Length - 1; i >= 0; i--) {
            rc[i] = best.Dequeue();
        }
        return rc;
    }
}
