using ImogeIndex.Mur;
using ImogeIndex.Storage;

namespace ImogeIndex.Web6.Dictators;

/// <summary>
/// ﾕーｻﾞー定義ﾗﾍﾞﾙの付け外し
/// </summary>
public sealed class LabelDictator
{
    /// <summary>
    /// 付け外しが前後しないようｼﾘｱﾙに
    /// </summary>
    readonly SemaphoreSlim applyLock = new(1, 1);

    /// <summary>
    /// ﾗﾍﾞﾙの付け外し(ﾗﾍﾞﾙはidではなくﾌｧｲﾙﾊｯｼｭで紐付く所に注意)
    /// </summary>
    /// <param name="targets">対象</param>
    /// <param name="label">nullか空白なら外す</param>
    /// <param name="items">ﾃﾞーﾀﾑの全項目</param>
    /// <param name="token"></param>
    /// <returns>ﾗﾍﾞﾙが書き換わった項目</returns>
    public async Task<IReadOnlyList<FileItem>> ApplyAsync(
        IReadOnlyList<FileItem> targets, string? label, IReadOnlyList<FileItem> items, CancellationToken token)
    {
        if (targets.Count == 0) {
            return [];
        }
        var value = string.IsNullOrWhiteSpace(label) ? null : label.Trim();
        await applyLock.WaitAsync(token);
        try {
            var affected = await Task.Run(() => {
                var files = IndexStore.Instance.GetSameHashFiles(targets.Select(x => x.Id), token);
                LabelStore.Instance.SetLabel(files.Values, value, token);
                var ids = new IdSet();
                foreach (var id in files.Keys) {
                    ids.Add(id);
                }
                var found = new List<FileItem>();
                foreach (var item in items) {
                    if (ids.Contains(item.Id)) {
                        found.Add(item);
                    }
                }
                return found;
            }, token);
            foreach (var item in affected) {
                item.Label = value;
            }
            return affected;
        }
        finally {
            applyLock.Release();
        }
    }
}
