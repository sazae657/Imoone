using ImogeIndex.Common;
using System;
using System.Collections.Generic;
using System.Text;

namespace ImogeIndex.Web2;

/// <summary>
/// DateTimeExtensions
/// </summary>
public class DateTimeExtensionsController {

    [Theory(DisplayName = "long日付")]
    [InlineData("1885-01-01T12:00:00", -2682277200)]
    [InlineData("1985-10-26T10:39:00", 499138740)]
    [InlineData("2015-10-21T16:29:00", 1445412540)]
    public void AsUnixTimeSeconds1(string dt, long expected) {
        var d = DateTime.Parse(dt);
        Assert.Equal(expected, d.AsUnixTimeSeconds());
    }
}
