using ImogeIndex.Common;
using System;
using System.Collections.Generic;
using System.Text;

namespace ImogeIndex.Web2;

/// <summary>
/// FileSizeExtensions
/// </summary>
public class IntegerExtensionsController {

    [Theory(DisplayName = "読みやすいバイト数")]
    [InlineData(0, "0 B")]
    [InlineData(1024, "1 KB")]
    [InlineData(1048576, "1 MB")]
    [InlineData(1073741824, "1 GB")]
    [InlineData(1099511627776, "1 TB")]
    [InlineData(1125899906842624, "1 PB")]
    [InlineData(1152921504606846976, "1 EB")]
    public void SizeOf(long sz, string expected) {
        Assert.Equal(sz.ToReadableSize(), expected);
    }

    [Theory(DisplayName = "ポジる")]
    [InlineData(0, 1)]
    [InlineData(1, 1)]
    [InlineData(-1, 1)]
    public void Positive(int sz, int expected)
    {
        Assert.Equal(sz.Positive(), expected);
    }
}
