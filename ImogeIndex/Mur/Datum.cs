using ImogeIndex.Storage;
using System.Diagnostics.CodeAnalysis;

namespace ImogeIndex.Mur;

/// <summary>
/// 読み込みの段階(表示文言はUI側で当てる)
/// </summary>
public enum DatumLoadPhase {
    Sanitize,
    Allocating,
    Allocated,
    Loading,
    Indexing,
    Complete,
}

/// <summary>
/// 進捗ﾃﾞﾘｹﾞーﾃｨー
/// </summary>
/// <param name="phase"></param>
/// <param name="p"></param>
public delegate void ProgressDelegaty(DatumLoadPhase phase, double p);

/// <summary>
/// いもげ
/// </summary>
public class ImogeDatum {
    /// <summary>
    /// 項目とｲﾝﾃﾞｯｸｽの一式(作ったら書き換えない)
    /// </summary>
    sealed class Generation {
        public Generation(FileItem[] items, SearchIndices indices) {
            Items = items;
            Indices = indices;
            StaleAliases = indices.StaleAliases.ToArray();
        }

        public FileItem[] Items { get; }
        public SearchIndices Indices { get; }
        public string[] StaleAliases { get; }
    }

    Generation current;

    public ImogeDatum() : this(new Generation([], new SearchIndices())) { }

    ImogeDatum(Generation generation) {
        current = generation;
    }

    Generation Current => Volatile.Read(ref current);

    /// <summary>
    /// 項目
    /// </summary>
    public IReadOnlyList<FileItem> Items => Current.Items;

    /// <summary>
    /// ｽﾅｯﾌﾟｼｮｯﾄ取得
    /// </summary>
    /// <returns></returns>
    public ImogeDatum Capture() => new(Current);

    /// <summary>
    /// 世代の同一性(同じ世代なら同じ参照)
    /// </summary>
    internal object Version => Current;

    /// <summary>
    /// 設定にあるのに検索ｲﾝﾃﾞｯｸｽが古いので使えないﾒﾀﾃﾞーﾀﾑのｴｲﾘｱｽ
    /// </summary>
    public IReadOnlyList<string> StaleMetadataAliases => Current.StaleAliases;

    /// <summary>
    /// 検索に使える数値ｲﾝﾃﾞｯｸｽ取得
    /// </summary>
    /// <param name="tag"></param>
    /// <param name="index"></param>
    /// <returns></returns>
    public bool TryGetNumericMetadataIndex(string tag, [MaybeNullWhen(false)] out NumericMetadataIndex index)
        => Current.Indices.NumericMetadata.TryGetValue(tag, out index);

    /// <summary>
    /// 検索に使える転置ｲﾝﾃﾞｯｸｽ取得
    /// </summary>
    /// <param name="field"></param>
    /// <param name="index"></param>
    /// <returns></returns>
    public bool TryGetIndex(SearchField field, [MaybeNullWhen(false)] out NgramInvertedIndex index) {
        if (Current.Indices.Fields.TryGetValue(field, out var found)) {
            index = found;
            return true;
        }
        index = null;
        return false;
    }

    /// <summary>
    /// ﾒﾀﾃﾞーﾀﾑのｲﾝﾃﾞｯｸｽ取得
    /// </summary>
    /// <param name="tag"></param>
    /// <param name="index"></param>
    /// <returns></returns>
    public bool TryGetMetadataIndex(
        string tag, [MaybeNullWhen(false)] out NgramInvertedIndex index) {
        if (Current.Indices.Metadata.TryGetValue(tag, out var found)) {
            index = found;
            return true;
        }
        index = null;
        return false;
    }

    /// <summary>
    /// 検索ｲﾝﾃﾞｯｸｽ読み込み
    /// </summary>
    /// <param name="data"></param>
    /// <param name="maxImageId"></param>
    /// <param name="metadataHash"></param>
    /// <param name="token"></param>
    /// <param name="aliases">設定中のｴｲﾘｱｽ(古いｲﾝﾃﾞｯｸｽの検出用)</param>
    internal void LoadSearchIndices(SearchIndexDataum data, long maxImageId, long metadataHash,
        CancellationToken token = default, IEnumerable<MetaAlias>? aliases = null) {
        var built = BuildSearchIndices(data, maxImageId, metadataHash, aliases, token);
        Volatile.Write(ref current, new Generation(Current.Items, built));
    }

    /// <summary>
    /// 組み上げた検索ｲﾝﾃﾞｯｸｽ一式
    /// </summary>
    sealed class SearchIndices {
        public Dictionary<SearchField, NgramInvertedIndex> Fields { get; } = new();
        public Dictionary<string, NgramInvertedIndex> Metadata { get; } =
            new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, NumericMetadataIndex> NumericMetadata { get; } =
            new(StringComparer.OrdinalIgnoreCase);
        public List<string> StaleAliases { get; } = [];
    }

    /// <summary>
    /// 検索ｲﾝﾃﾞｯｸｽを組み上げる
    /// </summary>
    /// <param name="data"></param>
    /// <param name="maxImageId"></param>
    /// <param name="metadataHash"></param>
    /// <param name="aliases"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    static SearchIndices BuildSearchIndices(SearchIndexDataum data, long maxImageId, long metadataHash,
        IEnumerable<MetaAlias>? aliases, CancellationToken token) {
        var built = new SearchIndices();
        foreach (var pair in NumericMetadataIndex.Sanitize(data, maxImageId, metadataHash, token)) {
            built.NumericMetadata.Add(pair.Key, pair.Value);
        }
        var loaded = NgramInvertedIndex.Sanitize(data, maxImageId, metadataHash, token);
        (SearchField Field, string Key)[] fields = [
            (SearchField.Tag, "tag"), (SearchField.Character, "character"),
            (SearchField.Keyword, "kwd"), (SearchField.Costume, "costume"),
            (SearchField.Objects, "object"),
        ];
        foreach (var field in fields) {
            if (loaded.TryGetValue(field.Key, out var index)) {
                built.Fields.Add(field.Field, index);
            }
        }
        foreach (var pair in loaded) {
            if (pair.Key.StartsWith("meta:", StringComparison.Ordinal)) {
                built.Metadata.Add(pair.Key[5..], pair.Value);
            }
        }
        if (aliases != null) {
            // TagIndexProzessorと同じ条件で、作られているはずのｴｲﾘｱｽを洗い出す
            foreach (var gr in aliases
                .Where(x => !string.IsNullOrWhiteSpace(x.Alias) && !string.IsNullOrWhiteSpace(x.Tag))
                .GroupBy(x => x.Alias.Trim(), StringComparer.OrdinalIgnoreCase)) {
                var found = gr.First().ValueType == MetadataValueType.Number
                    ? built.NumericMetadata.ContainsKey(gr.Key)
                    : built.Metadata.ContainsKey(gr.Key);
                if (!found) {
                    built.StaleAliases.Add(gr.Key);
                }
            }
        }
        return built;
    }

    /// <summary>
    /// ｲﾝﾃﾞｯｸｽ読み込み(最後まで読めた時だけ既存の内容を差し替える)
    /// </summary>
    /// <param name="delegaty"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    /// <exception cref="OperationCanceledException"></exception>
    public async Task Load(ProgressDelegaty delegaty, CancellationToken token) {
        await Task.Run(()=> {
            token.ThrowIfCancellationRequested();
            delegaty(DatumLoadPhase.Allocating, 0);
            var cn = IndexStore.Instance.GetImogeCount();
            var items = new List<FileItem>(Math.Max(cn, 0));
            long dd = 0;
            long maxRecordId = -1;
            // 塊ごとにｽﾚﾌﾟへ投げて読み取りと並行させる(並び順は塊の順で保つ)
            var batches = new List<Task<FileItem[]>>();
            var batch = new List<FileRecord>(ParseBatchSize);
            // ﾗﾍﾞﾙはﾊｯｼｭで紐付いてるので、FileItem化する時に一緒に当てる
            IReadOnlyDictionary<string, string>? labels = LabelStore.IsOpened ? LabelStore.Instance.LoadAll(token) : null;
            delegaty(DatumLoadPhase.Allocated, 0);
            try {
                foreach (var x in IndexStore.Instance.QueryAll(token, ["container", "path"])) {
                    token.ThrowIfCancellationRequested();
                    dd++;
                    // 集計の世代比較に使うので読み飛ばすﾚｺーﾄﾞのIDも見ておく
                    if (x.Id > maxRecordId) {
                        maxRecordId = x.Id;
                    }
                    batch.Add(x);
                    if (batch.Count == ParseBatchSize) {
                        batches.Add(ParseBatchAsync(batch, labels));
                        batch = new List<FileRecord>(ParseBatchSize);
                    }
                    if ((dd % 1000) == 0) {
                        delegaty(DatumLoadPhase.Loading, (double)dd / cn);
                    }
                }
                if (batch.Count != 0) {
                    batches.Add(ParseBatchAsync(batch, labels));
                }
            }
            catch (ObjectDisposedException ex) when (token.IsCancellationRequested) {
                // たぶん遅延評価中に殺された
                throw new OperationCanceledException(null, ex, token);
            }

            // ﾌｫﾙﾀﾞー名は項目間で大量に被るので共有させる
            var pool = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var parsed in batches) {
                foreach (var item in parsed.GetAwaiter().GetResult()) {
                    item.ShareStrings(pool);
                    items.Add(item);
                }
            }
            token.ThrowIfCancellationRequested();

            SearchIndices built;
            try {
                delegaty(DatumLoadPhase.Indexing, 1.0);
                var aliases = Profile.Active.MetadataSearchTags;
                built = BuildSearchIndices(IndexStore.Instance.GetSearchIndex(token), maxRecordId,
                    MetadataIndexMarker.CalculateTagHash(aliases), aliases, token);
            }
            catch (ObjectDisposedException ex) when (token.IsCancellationRequested) {
                // たぶん遅延評価中に殺された
                throw new OperationCanceledException(null, ex, token);
            }
            token.ThrowIfCancellationRequested();

            // 全部揃ったので世代ごと差し替える(検索中のｽﾚｯﾄﾞは古い世代をそのまま)
            Volatile.Write(ref current, new Generation(items.ToArray(), built));

            delegaty(DatumLoadPhase.Complete, 0);
        }, token);
    }

    /// <summary>
    /// FileItem化する塊の件数
    /// </summary>
    const int ParseBatchSize = 4096;

    /// <summary>
    /// 塊をまとめてFileItem化
    /// </summary>
    /// <param name="records"></param>
    /// <param name="labels">ﾌｧｲﾙﾊｯｼｭ→ﾗﾍﾞﾙ</param>
    /// <returns></returns>
    private static Task<FileItem[]> ParseBatchAsync(List<FileRecord> records, IReadOnlyDictionary<string, string>? labels)
        => Task.Run(() => {
            var result = new List<FileItem>(records.Count);
            foreach (var x in records) {
                if (FileItem.Parse(x, labels) is { } item) {
                    result.Add(item);
                }
            }
            return result.ToArray();
        });
}
