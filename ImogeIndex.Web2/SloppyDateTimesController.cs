
using ImogeIndex.Common;

namespace ImogeIndex.Web2;

/// <summary>
/// SloppyDateTime
/// </summary>
public class SloppyDateTimesController
{
    [Theory(DisplayName ="EXIFで遭遇したひどい日付達")]
    [InlineData("2000:01:02 03:04:05")]
    [InlineData("2000:01:02 3:04:05")]
    [InlineData("2000:1:2 3:04:05")]
    [InlineData("2000:1:2 3:4:5")]
    [InlineData("2000:01:02:03:04:05")]
    [InlineData("2000/01/02 03:04:05")]
    [InlineData("2000/01/02/03:04:05")]
    [InlineData("2000/01/02 26:04:05")]
    [InlineData("2000-01-02T03:04:05+16:00")]
    public void Parse1(string dt)
    {
        Assert.True(SloppyDateTime.TryParse(dt, out var _));
    }

    [Theory(DisplayName = "解釈できない日付は例外を投げずにfalse")]
    [InlineData("2020:13:40 25:00:00")]
    [InlineData("2020:02:30 25:00:00")]
    [InlineData("9999:12:31 99:99:99")]
    [InlineData("2020-13-40T00:00:00+16:00")]
    [InlineData("9999-12-31T23:59:59+99:00")]
    [InlineData("2020:13:40")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void ParseInvalid(string? dt)
    {
        Assert.False(SloppyDateTime.TryParse(dt, out var result));
        Assert.Equal(default, result);
    }

    [Fact(DisplayName = "25時超えは翌日に繰り越す")]
    public void ParseOverflowHour()
    {
        Assert.True(SloppyDateTime.TryParse("2000/01/02 26:04:05", out var result));
        Assert.Equal(new DateTime(2000, 1, 3, 2, 4, 5), result);
    }
}
