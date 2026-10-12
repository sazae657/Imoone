using System.Globalization;
using System.Text.RegularExpressions;

namespace ImogeIndex.Common;

/// <summary>
/// EXIFのﾌﾘーﾀﾞﾑな日付をなんとかする
/// </summary>
public static partial class SloppyDateTime
{
    /// <summary>
    /// 今の所出現を確認した日付ﾌｫーﾏｯﾄ
    /// </summary>
    static readonly string?[] ExifDtimeFormats = [
        "yyyy:MM:dd HH:mm:ss",
        "yyyy:MM:dd H:mm:ss",
        "yyyy:M:d H:mm:ss",
        "yyyy:M:d H:m:s",
        "yyyy:MM:dd:HH:mm:ss",
        "yyyy/MM/dd HH:mm:ss",
        "yyyy/MM/dd/HH:mm:ss",
        "yyyy/MM/dd/HH/mm/ss",
        null];

    /// <summary>
    /// Exifのﾌﾘーﾀﾞﾑな日付をDateTimeに
    /// </summary>
    /// <param name="dt"></param>
    /// <param name="dateTime"></param>
    /// <returns></returns>
    public static bool TryParse(string? dt, out DateTime dateTime)
    {
        dateTime = default;
        if (string.IsNullOrWhiteSpace(dt)) {
            return false;
        }
        if (ParseDateTimeOf(dt, out dateTime)) {
            return true;
        }
        if (!TryFixDateTimeStr(dt, out var ff, out var duration)) {
            dateTime = default;
            return false;
        }
        if (!ParseDateTimeOf(ff, out var fixedTime)) {
            dateTime = default;
            return false;
        }
        if (duration != null) {
            try {
                fixedTime = fixedTime.Add(duration.Value);
            }
            catch (ArgumentOutOfRangeException) {
                // DateTimeの範囲外に飛んだ
                dateTime = default;
                return false;
            }
        }
        dateTime = fixedTime;
        return true;
    }

    /// <summary>
    /// 日付
    /// </summary>
    /// <param name="str"></param>
    /// <param name="dt"></param>
    /// <returns></returns>
    static bool ParseDateTimeOf(string str, out DateTime dt)
    {
        if (DateTime.TryParse(str, out dt)) {
            return true;
        }

        foreach (var f in ExifDtimeFormats) {
            if (f == null) {
                if (DateTime.TryParse(str, out dt)) {
                    return true;
                }
            }
            else if (DateTime.TryParseExact(str, f, CultureInfo.InvariantCulture, DateTimeStyles.None, out dt)) {
                return true;
            }
        }
        dt = default;
        return false;
    }

    // 恐らく火星辺りの日付を示しているであろう15時間超えのﾀｲﾑｿﾞーﾝ
    [GeneratedRegex(@"\A(\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2})([+-])(\d{2}):(\d{2})\Z")]
    private static partial Regex InvDFM();

    // どこのﾃﾞｼﾞｶﾒか忘れたけど:/混在かつ25時超えの時刻を平気でExifに記録するやつ
    [GeneratedRegex(@"\A(\d{4})[/:](\d{2})[/:](\d{2}) ([2-9][0-9]):(\d{2}):(\d{2})\Z")]
    private static partial Regex InvDTM();

    /// <summary>
    /// 明らかにおかしい日付文字列を補正
    /// </summary>
    /// <param name="dt"></param>
    /// <param name="result">補正後の文字列</param>
    /// <param name="duration"></param>
    /// <returns>補正できないならfalse</returns>
    static bool TryFixDateTimeStr(string dt, out string result, out TimeSpan? duration) {
        result = dt;
        duration = null;
        var imc = InvDTM().Match(result);
        if (imc.Success) {
            // 24時超えの時刻対応
            var year = int.Parse(imc.Groups[1].Value, CultureInfo.InvariantCulture);
            var month = int.Parse(imc.Groups[2].Value, CultureInfo.InvariantCulture);
            var day = int.Parse(imc.Groups[3].Value, CultureInfo.InvariantCulture);

            var hour = int.Parse(imc.Groups[4].Value, CultureInfo.InvariantCulture);
            var minute = int.Parse(imc.Groups[5].Value, CultureInfo.InvariantCulture);
            var second = int.Parse(imc.Groups[6].Value, CultureInfo.InvariantCulture);
            if (!IsValidDate(year, month, day)) {
                return false;
            }
            DateTime correct;
            try {
                correct = new DateTime(year, month, day)
                        .AddHours(hour)
                        .AddMinutes(minute)
                        .AddSeconds(second);
            }
            catch (ArgumentOutOfRangeException) {
                // 9999年末から溢れた
                return false;
            }

            result = correct.ToString("yyyy:MM:dd HH:mm:ss", CultureInfo.InvariantCulture);
        }
        var mmc = InvDFM().Match(result);
        if (mmc.Success) {
            // 地球外のｵﾌｾｯﾄを地球のｵﾌｾｯﾄに
            if (!DateTime.TryParse(mmc.Groups[1].Value, CultureInfo.InvariantCulture, DateTimeStyles.None, out _)) {
                return false;
            }
            var sign = mmc.Groups[2].Value == "-" ? -1 : 1;
            var hours = int.Parse(mmc.Groups[3].Value, CultureInfo.InvariantCulture);
            var minutes = int.Parse(mmc.Groups[4].Value, CultureInfo.InvariantCulture);
            duration = TimeSpan.FromMinutes(sign * (hours * 60 + minutes));

            // 現在のところ15月とか48時93分-69秒のような火星辺りの日時は観測されていないのでそのまま
            result = mmc.Groups[1].Value;
        }
        return true;
    }

    /// <summary>
    /// 実在する年月日？
    /// </summary>
    /// <param name="year"></param>
    /// <param name="month"></param>
    /// <param name="day"></param>
    /// <returns></returns>
    static bool IsValidDate(int year, int month, int day)
    {
        if (year < DateTime.MinValue.Year || year > DateTime.MaxValue.Year) {
            return false;
        }
        if (month < 1 || month > 12) {
            return false;
        }
        return day >= 1 && day <= DateTime.DaysInMonth(year, month);
    }
}
