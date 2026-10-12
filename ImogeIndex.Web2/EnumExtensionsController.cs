using ImogeIndex.Common;
using ImogeIndex.Storage;
using System;
using System.Collections.Generic;
using System.Text;

namespace ImogeIndex.Web2;


/// <summary>
/// EnumExtensions
/// </summary>
public class EnumExtensionsController {
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Field)]
    class 菌Attribute : Attribute {
    }

    enum HyperValues {
        [菌]
        かゆい,
        うま
    }

    [Fact(DisplayName = "属性あるよね")]
    public void HasAttribute1() {
        Assert.True(HyperValues.かゆい.HasAttribute<菌Attribute>());
    }

    [Fact(DisplayName = "属性ないよね")]
    public void HasAttribute2() {
        Assert.False(HyperValues.うま.HasAttribute<菌Attribute>());
    }

    [Fact(DisplayName = "属性が取れる")]
    public void GetAttrubute1() {
        var attr = HyperValues.かゆい.GetAttrubute<菌Attribute>();
        Assert.NotNull(attr);
        Assert.IsType<菌Attribute>(attr);
    }

    [Fact(DisplayName = "属性が取れぬ")]
    public void GetAttrubute3() {
        var attr = HyperValues.うま.GetAttrubute<菌Attribute>();
        Assert.Null(attr);
    }

    [Fact(DisplayName = "属性が取れるかもしれない")]
    public void TryGetAttribute1() {
        Assert.True(HyperValues.かゆい.TryGetAttribute<菌Attribute>(out var attr));
        Assert.NotNull(attr);
        Assert.IsType<菌Attribute>(attr);
    }

    [Fact(DisplayName = "属性が取れないかもしれない")]
    public void TryGetAttribute2() {
        Assert.False(HyperValues.うま.TryGetAttribute<菌Attribute>(out var attr));
        Assert.Null(attr);
    }
}
