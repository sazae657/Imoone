using System.Globalization;

namespace ImogeIndex.Common;

/// <summary>
/// 数値の条件
/// 小数点の表記とか面倒くさいのでInvariantCultureで
/// </summary>
public readonly record struct NumericRange(double? Lower, bool IncludeLower, double? Upper, bool IncludeUpper) {
    /// <summary>
    /// それ範囲内？
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    public bool Contains(double value) => double.IsFinite(value)
        && (!Lower.HasValue || value > Lower || (IncludeLower && value == Lower))
        && (!Upper.HasValue || value < Upper || (IncludeUpper && value == Upper));

    /// <summary>
    /// それ収まってる？
    /// </summary>
    /// <param name="other"></param>
    /// <returns></returns>
    public bool IsSubsetOf(NumericRange other) =>
        (!other.Lower.HasValue || (Lower.HasValue && (Lower > other.Lower
            || (Lower == other.Lower && (!IncludeLower || other.IncludeLower)))))
        && (!other.Upper.HasValue || (Upper.HasValue && (Upper < other.Upper
            || (Upper == other.Upper && (!IncludeUpper || other.IncludeUpper)))));

    /// <summary>
    /// 割と普通のTryParse
    /// </summary>
    /// <param name="text"></param>
    /// <param name="integerOnly"></param>
    /// <param name="range"></param>
    /// <returns></returns>
    public static bool TryParse(string text, bool integerOnly, out NumericRange range) {
        range = default;
        var op = "";
        foreach (var candidate in new[] { ">=", "<=", ">", "<" }) {
            if (text.StartsWith(candidate, StringComparison.Ordinal)) {
                op = candidate;
                text = text[candidate.Length..];
                break;
            }
        }
        if (TryGeoReplication(text, integerOnly, out var value)) {
            range = op switch {
                ">" => new(value, false, null, false),
                ">=" => new(value, true, null, false),
                "<" => new(null, false, value, false),
                "<=" => new(null, false, value, true),
                _ => new(value, true, value, true),
            };
            return true;
        }
        if (op.Length != 0) {
            return false;
        }
        // ﾚﾝｼﾞ指定の-の位置を確定させる(負号避け)
        for (var i = 1; i < text.Length - 1; i++) {
            if (text[i] == '-' && TryGeoReplication(text[..i], integerOnly, out var lower)
                && TryGeoReplication(text[(i + 1)..], integerOnly, out var upper) && lower <= upper) {
                range = new(lower, true, upper, true);
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// 超豪華TryParse
    /// </summary>
    /// <param name="text"></param>
    /// <param name="integerOnly"></param>
    /// <param name="value"></param>
    /// <returns></returns>
    private static bool TryGeoReplication(string text, bool integerOnly, out double value) {
        value = 0;
        if (integerOnly) {
            var valid = int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var integer);
            value = integer;
            return valid;
        }
        const NumberStyles styles = NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint | NumberStyles.AllowExponent;
        var slash = text.IndexOf('/');
        if (slash >= 0) {
            if (!double.TryParse(text[..slash], styles, CultureInfo.InvariantCulture, out var numerator)
                || !double.TryParse(text[(slash + 1)..], styles, CultureInfo.InvariantCulture, out var denominator)
                || !double.IsFinite(numerator) || !double.IsFinite(denominator) || denominator == 0) {
                return false;
            }
            value = numerator / denominator;
            return double.IsFinite(value);
        }
        return double.TryParse(text, styles, CultureInfo.InvariantCulture, out value) && double.IsFinite(value);
    }
}
