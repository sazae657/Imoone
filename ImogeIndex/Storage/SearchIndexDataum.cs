using ImogeIndex.Common;

namespace ImogeIndex.Storage;


/// <summary>
/// 通常ﾀｸﾞとﾒﾀﾃﾞーﾀﾑで共用する検索ｲﾝﾃﾞｯｸｽ
/// </summary>
public sealed class SearchIndexDefinition {
    public string Field { get; set; } = string.Empty;
    public int GramSize { get; set; }
    public MetadataValueType ValueType { get; set; }
    public long Version { get; set; } = SearchIndexDataum.CurrentVersion;
    public long MaxImageId { get; set; }
    public long ConfigurationHash { get; set; }
}

/// <summary>
/// あたい
/// </summary>
public sealed class SearchIndexValue {
    public string Field { get; set; } = string.Empty;
    public long ValueId { get; set; }
    public string Value { get; set; } = string.Empty;
    public long[] ImageIds { get; set; } = [];
}

/// <summary>
/// N-gramの方
/// </summary>
public sealed class SearchIndexGram {
    public string Field { get; set; } = string.Empty;
    public string Gram { get; set; } = string.Empty;
    public long[] ValueIds { get; set; } = [];
}

/// <summary>
/// 検索ｲﾝﾃﾞｯｸｽ
/// </summary>
public sealed class NumericSearchIndexValue {
    public string Field { get; set; } = string.Empty;
    public double Value { get; set; }
    public long[] ImageIds { get; set; } = [];
}

/// <summary>
/// 検索ｲﾝﾃﾞｯｸｽのﾃﾞーﾀﾑ
/// </summary>
public sealed class SearchIndexDataum {
    public const long CurrentVersion = 2;
    public List<SearchIndexDefinition> Definitions { get; } = [];
    public List<SearchIndexValue> Values { get; } = [];
    public List<SearchIndexGram> Grams { get; } = [];
    public List<NumericSearchIndexValue> NumericValues { get; } = [];

    public void AddNumericField(string field, IEnumerable<KeyValuePair<double, HashSet<long>>> source,
        long maxImageId, long configurationHash, CancellationToken token) {
        var values = new SortedDictionary<double, HashSet<long>>();
        foreach (var entry in source) {
            token.ThrowIfCancellationRequested();
            if (!double.IsFinite(entry.Key)) {
                continue;
            }
            if (!values.TryGetValue(entry.Key, out var ids)) {
                values.Add(entry.Key, ids = []);
            }
            ids.UnionWith(entry.Value);
        }
        foreach (var entry in values) {
            token.ThrowIfCancellationRequested();
            NumericValues.Add(new NumericSearchIndexValue {
                Field = field, Value = entry.Key, ImageIds = entry.Value.Order().ToArray(),
            });
        }
        token.ThrowIfCancellationRequested();
        Definitions.Add(new SearchIndexDefinition {
            Field = field, ValueType = MetadataValueType.Number,
            MaxImageId = maxImageId, ConfigurationHash = configurationHash,
        });
    }

    /// <summary>
    /// ﾒﾀﾃﾞーﾀﾑのﾌｨーﾙﾄﾞ名
    /// </summary>
    /// <param name="alias"></param>
    /// <returns></returns>
    public static string MetadataField(string alias) => "meta:" + alias.Trim().ToLowerInvariant();

    /// <summary>
    /// N取得
    /// </summary>
    /// <param name="field"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    public static int GramSizeOf(string field) => field switch {
        "tag" or "character" => 3,
        "kwd" or "costume" or "object" => 2,
        _ when field.StartsWith("meta:", StringComparison.Ordinal) => 3,
        _ => throw new ArgumentException(Properties.Resources.SM_E_UnsupportedSearchField.Morfat(field), nameof(field)),
    };


    /// <summary>
    /// 小文字に揃えて片割れのｻﾛｹﾞーﾄはU+FFFDに寄せる
    /// (不正なUTF-16はDB往復でU+FFFDに化けるので、索引側と検索側で先に揃えておく)
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    public static string Normalize(string? value) {
        if (value is null) {
            return string.Empty;
        }
        return ReplaceLoneSurrogates(value.ToLowerInvariant());
    }

    /// <summary>
    /// 片割れのｻﾛｹﾞーﾄをU+FFFDに置き換える(無ければそのまま返す)
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    static string ReplaceLoneSurrogates(string value) {
        var first = FindLoneSurrogate(value, 0);
        if (first < 0) {
            return value;
        }
        var chars = value.ToCharArray();
        for (var i = first; i >= 0; i = FindLoneSurrogate(value, i + 1)) {
            chars[i] = '�';
        }
        return new string(chars);
    }

    /// <summary>
    /// start以降で最初の片割れｻﾛｹﾞーﾄの位置
    /// </summary>
    /// <param name="value"></param>
    /// <param name="start"></param>
    /// <returns>無ければ-1</returns>
    static int FindLoneSurrogate(string value, int start) {
        for (var i = start; i < value.Length; i++) {
            if (!char.IsSurrogate(value[i])) {
                continue;
            }
            if (char.IsSurrogatePair(value, i)) {
                i++;
                continue;
            }
            return i;
        }
        return -1;
    }

    /// <summary>
    /// ｺーﾄﾞﾎﾟｲﾝﾄ数(ｻﾛｹﾞーﾄﾍﾟｱは1文字として数える)
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    public static int CodePointLength(string value) {
        var count = 0;
        for (var i = 0; i < value.Length; i += char.IsSurrogatePair(value, i) ? 2 : 1) {
            count++;
        }
        return count;
    }

    /// <summary>
    /// ｺーﾄﾞﾎﾟｲﾝﾄ単位で重複しないgramを列挙
    /// </summary>
    /// <param name="value"></param>
    /// <param name="size"></param>
    /// <returns></returns>
    public static IEnumerable<string> InternetGrams(string value, int size) {
        ArgumentOutOfRangeException.ThrowIfLessThan(size, 1);
        // 各ｺーﾄﾞﾎﾟｲﾝﾄの開始位置
        var starts = new List<int>(value.Length + 1);
        for (var i = 0; i < value.Length; i += char.IsSurrogatePair(value, i) ? 2 : 1) {
            starts.Add(i);
        }
        starts.Add(value.Length);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var k = 0; k + size < starts.Count; k++) {
            var gram = value[starts[k]..starts[k + size]];
            if (seen.Add(gram)) {
                yield return gram;
            }
        }
    }

    /// <summary>
    /// ﾌｨーﾙﾄﾞ追加
    /// </summary>
    /// <param name="field"></param>
    /// <param name="source"></param>
    /// <param name="maxImageId"></param>
    /// <param name="configurationHash"></param>
    /// <param name="token"></param>
    public void AddField(string field,
        IEnumerable<KeyValuePair<string, HashSet<long>>> source,
        long maxImageId,
        long configurationHash,
        CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var size = GramSizeOf(field);
        var values = new SortedDictionary<string, HashSet<long>>(StringComparer.Ordinal);
        foreach (var x in source) {
            token.ThrowIfCancellationRequested();
            var value = Normalize(x.Key.Trim());
            if (value.Length == 0) {
                continue;
            }
            if (!values.TryGetValue(value, out var ids)) {
                values.Add(value, ids = []);
            }
            ids.UnionWith(x.Value);
        }

        var grams = new Dictionary<string, List<long>>(StringComparer.Ordinal);
        long valueId = 0;
        foreach (var x in values) {
            token.ThrowIfCancellationRequested();
            Values.Add(new SearchIndexValue {
                Field = field, ValueId = valueId, Value = x.Key,
                ImageIds = x.Value.Order().ToArray(),
            });
            foreach (var gram in InternetGrams(x.Key, size)) {
                token.ThrowIfCancellationRequested();
                if (!grams.TryGetValue(gram, out var ids)) {
                    grams.Add(gram, ids = []);
                }
                ids.Add(valueId);
            }
            valueId++;
        }
        foreach (var x in grams) {
            token.ThrowIfCancellationRequested();
            Grams.Add(new SearchIndexGram { Field = field, Gram = x.Key, ValueIds = x.Value.ToArray() });
        }
        // とりあえず空でも保存して0件確定できるようにしとく
        Definitions.Add(new SearchIndexDefinition {
            Field = field, GramSize = size, MaxImageId = maxImageId,
            ConfigurationHash = configurationHash,
        });
    }
}
