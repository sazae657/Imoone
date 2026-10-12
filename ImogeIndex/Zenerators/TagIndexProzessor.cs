using ImogeIndex.Common;
using ImogeIndex.Storage;

namespace ImogeIndex.Zenerators;


public class TagIndexingResult : IIndexingResult {
    public bool Success { get; set; } = true;
    public Exception? Exception { get; set; }
    public bool Cancelled { get; set; }
    public bool WasFailed() => !Success && !Cancelled;

    public int Groups { get; set; }
    public int Characters { get; set; }
}

/// <summary>
/// ﾀｸﾞお纏め生成
/// </summary>
public class TagIndexProzessor : IDisposable, IProzessor {
    public event MessageDelegaty? OnMessage;
    public event ProgressDelegaty? OnProgress;
    public event StateDelegaty? OnStart;
    public event StateDelegaty? OnComplete;
    public event ErrorDelegaty? OnError;

    readonly ExecutionStateObzekt execution = new();


    public bool Running => execution.Running;
    public event StateDelegaty? BeginVeryLongProcess;
    public event StateDelegaty? EndVeryLongProcess;


    Dictionary<string, HashSet<long>> tags = new();
    Dictionary<string, HashSet<long>> characters = new();
    Dictionary<string, HashSet<long>> keywords = new();
    Dictionary<string, HashSet<long>> costume = new();
    Dictionary<string, HashSet<long>> objects = new();

    /// <summary>
    /// 集計した時点での最大IDを世代情報に使う
    /// </summary>
    long maxImogeId = 0;

    public async Task<TagIndexingResult> Run(CancellationToken token) {
        using var run = execution.TryStart();
        if (run == null) {
            return new();
        }
        try {
            await Task.Run(() => {
                OnStart?.Invoke();
                if (!IndexStore.Instance.IsWritable) {
                    // 保存できないので集計前に止める
                    throw new InvalidOperationException(Properties.Resources.TP_E_ReadOnlyIndex);
                }
                var dataVersion = IndexStore.Instance.GetSearchDataVersion();
                CollectIndex(token);
                BeginVeryLongProcess?.Invoke();
                try {
                    SaveIndex(dataVersion, token);
                }
                finally {
                    EndVeryLongProcess?.Invoke();
                }
            }, token);
            return new TagIndexingResult { Groups = tags.Count, Characters = characters.Count };
        }
        catch (OperationCanceledException) {
            throw;
        }
        catch (Exception ex) {
            OnError?.Invoke(ex, Properties.Resources.ZG_Error.Morfat(nameof(TagIndexProzessor), ex.Message));
            return new TagIndexingResult { Success = false, Exception = ex };
        }
        finally {
            OnComplete?.Invoke();
        }
    }

    /// <summary>
    /// ｲﾝﾃﾞｯｸｽ保存
    /// </summary>
    /// <param name="dataVersion"></param>
    /// <param name="token"></param>
    void SaveIndex(long dataVersion, CancellationToken token) {
        token.ThrowIfCancellationRequested();
        OnMessage?.Invoke(Properties.Resources.CP_Saving);

        var data = new SearchIndexDataum();
        data.AddField("tag", tags, maxImogeId, 0, token);
        data.AddField("character", characters, maxImogeId, 0, token);
        data.AddField("kwd", keywords, maxImogeId, 0, token);
        data.AddField("costume", costume, maxImogeId, 0, token);
        data.AddField("object", objects, maxImogeId, 0, token);

        var aliases = Profile.Active.MetadataSearchTags.ToArray();
        var mhash = MetadataIndexMarker.CalculateTagHash(aliases);

        foreach (var gr in aliases
            .Where(x => !string.IsNullOrWhiteSpace(x.Alias) && !string.IsNullOrWhiteSpace(x.Tag))
            .GroupBy(x => x.Alias.Trim(), StringComparer.OrdinalIgnoreCase))
        {
            token.ThrowIfCancellationRequested();

            if (gr.First().ValueType == MetadataValueType.Number) {
                data.AddNumericField(SearchIndexDataum.MetadataField(gr.Key),
                    CollectNumericMetadataValues(gr, tag => IndexStore.Instance.GetMetadataOfTag(tag, token), token),
                    maxImogeId, mhash, token);
                continue;
            }
            var values = CollectMetadataValues(gr,
                tag => IndexStore.Instance.GetMetadataOfTag(tag, token));

            data.AddField(SearchIndexDataum.MetadataField(gr.Key), values,
                maxImogeId, mhash, token);
        }
        IndexStore.Instance.SaveSearchIndex(data, token, dataVersion);
    }

    /// <summary>
    /// 同じｴｲﾘｱｽに割り当てられた複数ﾀｸﾞをおまとめする
    /// </summary>
    /// <param name="aliases"></param>
    /// <param name="metadataProvider"></param>
    /// <returns></returns>
    internal static SortedDictionary<double, HashSet<long>> CollectNumericMetadataValues(
        IEnumerable<MetaAlias> aliases, Func<string, IEnumerable<MetadataRecord>> metadataProvider,
        CancellationToken token = default) {
        var values = new SortedDictionary<double, HashSet<long>>();
        foreach (var tag in aliases.Select(x => x.Tag.Trim()).Distinct(StringComparer.OrdinalIgnoreCase)) {
            foreach (var metadata in metadataProvider(tag)) {
                token.ThrowIfCancellationRequested();
                if (metadata.NumericValue is not double value || !double.IsFinite(value)) {
                    continue;
                }
                if (!values.TryGetValue(value, out var ids)) {
                    values.Add(value, ids = []);
                }
                ids.Add(metadata.ImageId);
            }
        }
        return values;
    }

    /// <summary>
    /// おまとめﾃﾞーﾀ作成
    /// </summary>
    /// <param name="aliases"></param>
    /// <param name="metadataProvider"></param>
    /// <returns></returns>
    internal static SortedDictionary<string, HashSet<long>> CollectMetadataValues(
        IEnumerable<MetaAlias> aliases,
        Func<string, IEnumerable<MetadataRecord>> metadataProvider) {
        var values = new SortedDictionary<string, HashSet<long>>(StringComparer.Ordinal);
        foreach (var tar in aliases
            .Select(x => x.Tag.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)) {
            foreach (var metadata in metadataProvider(tar)) {
                if (string.IsNullOrWhiteSpace(metadata.Value)) {
                    continue;
                }
                var value = SearchIndexDataum.Normalize(metadata.Value.Trim());
                if (!values.TryGetValue(value, out var ids)) {
                    ids = new HashSet<long>();
                    values.Add(value, ids);
                }
                ids.Add(metadata.ImageId);
            }
        }
        return values;
    }

    /// <summary>
    /// ｲﾝﾃﾞｯｸｽ読み込んで集計
    /// </summary>
    /// <param name="token"></param>
    void CollectIndex(CancellationToken token) {
        if (token.IsCancellationRequested) { 
            return; 
        }
        OnMessage?.Invoke(Properties.Resources.TP_Index);
        // 前回の集計を捨てる
        tags.Clear();
        characters.Clear();
        keywords.Clear();
        costume.Clear();
        objects.Clear();
        maxImogeId = 0;


        var imgc = IndexStore.Instance.GetImogeCount();
        int pc = 0;
        foreach (var x in IndexStore.Instance.QueryAll(token)) {
            pc++;
            if (token.IsCancellationRequested) {
                return;
            }
            if (x == null) {
                continue;
            }
            if (x.Id > maxImogeId) {
                maxImogeId = x.Id;
            }
            if ((pc % 100) == 0) {
                OnProgress?.Invoke(pc, imgc, 0, Properties.Resources.TP_ProcessingTag, x.Path);
            }
            // ﾀｸﾞとｷｬﾌﾟｼｮﾝは別々に付くのでﾌｨーﾙﾄﾞごとに独立して集計
            if (!string.IsNullOrEmpty(x.Tags)) {
                SanitizeDatum(tags, x.Id, x.Tags, token);
            }
            if (!string.IsNullOrEmpty(x.Characters)) {
                SanitizeDatum(characters, x.Id, x.Characters, token);
            }
            if (!string.IsNullOrEmpty(x.Keywords)) {
                SanitizeDatum(keywords, x.Id, x.Keywords, token);
            }
            if (!string.IsNullOrEmpty(x.Costume)) {
                SanitizeDatum(costume, x.Id, x.Costume, token);
            }
            if (!string.IsNullOrEmpty(x.Objects)) {
                SanitizeDatum(objects, x.Id, x.Objects, token);
            }
        }
    }

    /// <summary>
    /// mur生成
    /// </summary>
    /// <param name="datum"></param>
    /// <param name="id"></param>
    /// <param name="tag"></param>
    /// <param name="token"></param>
    void SanitizeDatum(Dictionary<string, HashSet<long>> datum, long id, string tag, CancellationToken token)
    {
        foreach (var t in tag.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim())) {
            if (token.IsCancellationRequested) {
                return;
            }
            var value = SearchIndexDataum.Normalize(t);
            if (value.Length == 0) {
                continue;
            }
            if (!datum.TryGetValue(value, out var ids)) {
                datum.Add(value, ids = []);
            }
            ids.Add(id);
        }
    }

    public Task WaitFor() => execution.WaitFor();



    #region IDisposable
    private bool disposedValue;

    protected virtual void Dispose(bool disposing) {
        if (!disposedValue) {
            disposedValue = true;
        }
    }

    public void Dispose() {
        // このｺーﾄﾞを変更しないでください。ｸﾘーﾝｱｯﾌﾟ ｺーﾄﾞを 'Dispose(bool disposing)' ﾒｿｯﾄﾞに記述します
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
    #endregion

}
