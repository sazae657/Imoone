
namespace ImogeIndex.Common;

/// <summary>
/// 日付拡張
/// </summary>
public static class DateTimeExtensions {

    /// <summary>
    /// Unix時間(秒)に
    /// </summary>
    /// <param name="date"></param>
    /// <returns></returns>
    public static long AsUnixTimeSeconds(this DateTime date) {
        return new DateTimeOffset(date).ToUnixTimeSeconds();
    }
}
