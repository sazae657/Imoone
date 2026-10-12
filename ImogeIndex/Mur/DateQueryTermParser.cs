using System.Globalization;

namespace ImogeIndex.Mur;

/// <summary>
/// 日付検索
/// dc:[&gt;|&lt;|&gt;=|&lt;=]yyyy-mm-ddThh:mm:ss
/// dc:yyyy-mm-ddThh:mm:ss-yyyy-mm-ddThh:mm:ss-
/// </summary>
internal sealed class DateQueryTerm : IQueryTerm
{
    readonly DateTime? lowerBound;
    readonly DateTime? upperBound;

    public DateQueryTerm(DateTime? lower, DateTime? upper)
    {
        lowerBound = lower;
        upperBound = upper;
    }

    public int Cost => 1;

    public bool IsMatch(FileItem item)
        => (!lowerBound.HasValue || item.DateTime >= lowerBound.Value)
            && (!upperBound.HasValue || item.DateTime < upperBound.Value);

    public bool TryResolveIds(ImogeDatum datum, IdSet destination) => false;

    public bool IsNarrowerThan(IQueryTerm other)
    {
        if (other is not DateQueryTerm date) {
            return false;
        }
        return IsLowerBoundNarrower(lowerBound, date.lowerBound)
            && IsUpperBoundNarrower(upperBound, date.upperBound);
    }

    private static bool IsLowerBoundNarrower(DateTime? value, DateTime? other)
        => !other.HasValue || (value.HasValue && value.Value >= other.Value);

    private static bool IsUpperBoundNarrower(DateTime? value, DateTime? other)
        => !other.HasValue || (value.HasValue && value.Value <= other.Value);
}

/// <summary>
/// dc:で始まる日付条件のﾊﾟーｻー
/// </summary>
internal sealed class DateQueryTermParser : IQueryTermParser
{
    private const string Prefix = "dc:";

    // TODO: 絶妙にﾀﾞｻい
    private static readonly (string Format, DatePart Part)[] Formats = [
        ("yyyy", DatePart.Year),
        ("yyyy-MM", DatePart.Month),
        ("yyyy-MM-dd", DatePart.Day),
        ("yyyy-MM-dd'T'HH", DatePart.Hour),
        ("yyyy-MM-dd'T'HH:mm", DatePart.Minute),
        ("yyyy-MM-dd'T'HH:mm:ss", DatePart.Second),
    ];

    public bool TryParse(string token, out IQueryTerm? term)
    {
        if (!token.StartsWith(Prefix, StringComparison.Ordinal)) {
            term = null;
            return false;
        }

        var condition = token[Prefix.Length..];
        // 解釈できない日付は捨てずに一致なしにする
        term = ParseComparison(condition) ?? ParseRange(condition) ?? InvalidQueryTerm.Instance;
        return true;
    }

    private static IQueryTerm? ParseComparison(string cond)
    {
        var qml = Cond.Eq;
        if (cond.StartsWith(">=", StringComparison.Ordinal)) {
            qml = Cond.Ge;
            cond = cond[2..];
        }
        else if (cond.StartsWith(">", StringComparison.Ordinal)) {
            qml = Cond.Gt;
            cond = cond[1..];
        }
        else if (cond.StartsWith("<=", StringComparison.Ordinal)) {
            qml = Cond.Le;
            cond = cond[2..];
        }
        else if (cond.StartsWith("<", StringComparison.Ordinal)) {
            qml = Cond.Lt;
            cond = cond[1..];
        }

        if (!TryParseDate(cond, out var start, out var end)) {
            return null;
        }
        return qml switch {
            Cond.Eq => new DateQueryTerm(start, end),
            Cond.Gt => new DateQueryTerm(end, null),
            Cond.Ge => new DateQueryTerm(start, null),
            Cond.Lt => new DateQueryTerm(null, start),
            Cond.Le => new DateQueryTerm(null, end),
            _ => null,
        };
    }

    private static IQueryTerm? ParseRange(string cond)
    {
        for (var i = 1; i < cond.Length - 1; i++) {
            if (cond[i] != '-') {
                continue;
            }
            if (TryParseDate(cond[..i], out var start, out _)
                && TryParseDate(cond[(i + 1)..], out _, out var end)) {
                return new DateQueryTerm(start, end);
            }
        }
        return null;
    }

    private static bool TryParseDate(string value, out DateTime start, out DateTime end)
    {
        foreach (var (format, part) in Formats) {
            if (!DateTime.TryParseExact(
                value,
                format,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out start)) {
                continue;
            }
            try {
                end = part switch {
                    DatePart.Year => start.AddYears(1),
                    DatePart.Month => start.AddMonths(1),
                    DatePart.Day => start.AddDays(1),
                    DatePart.Hour => start.AddHours(1),
                    DatePart.Minute => start.AddMinutes(1),
                    DatePart.Second => start.AddSeconds(1),
                    _ => start,
                };
                return true;
            }
            catch (ArgumentOutOfRangeException) {
                // DateTimeの上限を越える期間はｼｶﾄ
                break;
            }
        }
        start = default;
        end = default;
        return false;
    }

    private enum Cond
    {
        Eq,
        Gt,
        Ge,
        Lt,
        Le,
    }

    private enum DatePart
    {
        Year,
        Month,
        Day,
        Hour,
        Minute,
        Second,
    }
}
