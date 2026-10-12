using ImogeIndex.Common;

namespace ImogeIndex.Mur;

/// <summary>
/// meta:ﾀｸﾞ:値 形式のﾒﾀﾃﾞーﾀﾑ検索条件
/// </summary>
internal sealed class MetadataQueryTermParser : IQueryTermParser {
    const string Prefix = "meta:";
    readonly HashSet<string> numericTags;

    public MetadataQueryTermParser(IEnumerable<MetaAlias> aliases) {
        var configured = aliases.ToArray();
        MetaAlias.ValidateType(configured);
        numericTags = configured.Where(x => x.ValueType == MetadataValueType.Number)
            .Select(x => x.Alias.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    public bool TryParse(string token, out IQueryTerm? term) {
        if (!token.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase)) {
            term = null;
            return false;
        }
        // 不正な条件は捨てずに一致なしにする
        var ssr = token.IndexOf(':', Prefix.Length);
        if (ssr < 0) {
            term = InvalidQueryTerm.Instance;
            return true;
        }
        var tag = token[Prefix.Length..ssr].Trim();
        var value = Unquote(token[(ssr + 1)..]).Trim();
        if (numericTags.Contains(tag)) {
            term = NumericRange.TryParse(value, false, out var range)
                ? new NumericMetadataQueryTerm(tag, range)
                : InvalidQueryTerm.Instance;
            return true;
        }
        if (tag.Length == 0 || value.Length == 0) {
            term = InvalidQueryTerm.Instance;
            return true;
        }
        term = new MetadataQueryTerm(tag, value);
        return true;
    }

    private static string Unquote(string value) {
        if (value.Length >= 2 && value[0] == '"' && value[^1] == '"') {
            return value[1..^1];
        }
        return value;
    }
}


/// <summary>
/// 数値ｲﾝﾃﾞｯｸｽで解決する条件
/// </summary>
/// <param name="tag"></param>
/// <param name="range"></param>
internal sealed class NumericMetadataQueryTerm(string tag, NumericRange range) : IQueryTerm {
    public int Cost => 1;
    public bool IsMatch(FileItem item) => false;
    public bool TryResolveIds(ImogeDatum datum, IdSet destination) {
        // ｲﾝﾃﾞｯｸｽが無い(古い)時は一致なし、読み込み時にImogeDatum.StaleMetadataAliasesで知らせる
        if (datum.TryGetNumericMetadataIndex(tag, out var index)) {
            index.Lookup(range, destination);
        }
        return true;
    }
    public bool IsNarrowerThan(IQueryTerm other) => other is NumericMetadataQueryTerm number
        && string.Equals(tag, number.Tag, StringComparison.OrdinalIgnoreCase)
        && range.IsSubsetOf(number.Range);
    private string Tag => tag;
    private NumericRange Range => range;
}

/// <summary>
/// 指定ﾀｸﾞのﾒﾀﾃﾞーﾀﾑに対する部分一致条件
/// </summary>
internal sealed class MetadataQueryTerm : IQueryTerm {
    readonly string tag;
    readonly string value;

    public MetadataQueryTerm(string tag, string value) {
        this.tag = tag;
        this.value = FileItem.Normalize(value);
    }

    public int Cost => 1;

    // 展開するとすんごいﾒﾓﾘ食うのでﾒﾀﾃﾞーﾀﾑはFileItemへ展開せず必ず専用ｲﾝﾃﾞｯｸｽで解決
    public bool IsMatch(FileItem item) => false;

    public bool TryResolveIds(ImogeDatum datum, IdSet destination) {
        if (!datum.TryGetMetadataIndex(tag, out var index)) {
            // ｲﾝﾒﾓﾘでしか引けないので一致なし、読み込み時にImogeDatum.StaleMetadataAliasesで知らせる
            return true;
        }
        index.Lookup(value, destination);
        return true;
    }

    public bool IsNarrowerThan(IQueryTerm other) {
        return other is MetadataQueryTerm metadata
            && string.Equals(tag, metadata.tag, StringComparison.OrdinalIgnoreCase)
            && value.Contains(metadata.value, StringComparison.Ordinal);
    }
}
