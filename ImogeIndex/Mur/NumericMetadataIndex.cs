using ImogeIndex.Common;
using ImogeIndex.Storage;

namespace ImogeIndex.Mur;

/// <summary>
/// 数値ﾒﾀﾃﾞーﾀﾑのｲﾝﾃﾞｯｸｽ
/// </summary>
public sealed class NumericMetadataIndex {
    readonly NumericSearchIndexValue[] values;

    private NumericMetadataIndex(NumericSearchIndexValue[] values) => this.values = values;

    public static Dictionary<string, NumericMetadataIndex> Sanitize(SearchIndexDataum data,
        long maxImageId, long metadataHash, CancellationToken token)
    {
        var def = data.Definitions.Where(x => x.ValueType == ImogeIndex.MetadataValueType.Number
            && x.Field.StartsWith("meta:", StringComparison.Ordinal)
            && x.Version == SearchIndexDataum.CurrentVersion && x.MaxImageId >= maxImageId
            && x.ConfigurationHash == metadataHash).ToDictionary(x => x.Field, StringComparer.Ordinal);
        var gep = new Dictionary<string, List<NumericSearchIndexValue>>(StringComparer.Ordinal);
        foreach (var entry in data.NumericValues) {
            token.ThrowIfCancellationRequested();
            if (!def.ContainsKey(entry.Field) || !double.IsFinite(entry.Value)) {
                continue;
            }
            if (!gep.TryGetValue(entry.Field, out var list)) {
                gep.Add(entry.Field, list = []);
            }
            list.Add(entry);
        }
        var result = new Dictionary<string, NumericMetadataIndex>(StringComparer.OrdinalIgnoreCase);
        foreach (var field in def.Keys) {
            token.ThrowIfCancellationRequested();
            result.Add(field[5..], new NumericMetadataIndex(gep.TryGetValue(field, out var list)
                ? list.OrderBy(x => x.Value).ToArray() : []));
        }
        return result;
    }

    public void Lookup(NumericRange range, IdSet dest) {
        var start = range.Lower.HasValue ? Boundary(range.Lower.Value, !range.IncludeLower) : 0;
        var end = range.Upper.HasValue ? Boundary(range.Upper.Value, range.IncludeUpper) : values.Length;
        for (var i = start; i < end; i++) {
            dest.AddRange(values[i].ImageIds);
        }
    }

    private int Boundary(double value, bool afterEqual) {
        var left = 0;
        var right = values.Length;
        while (left < right) {
            var middle = left + (right - left) / 2;
            if (values[middle].Value < value || (afterEqual && values[middle].Value == value)) {
                left = middle + 1;
            }
            else {
                right = middle;
            }
        }
        return left;
    }
}
