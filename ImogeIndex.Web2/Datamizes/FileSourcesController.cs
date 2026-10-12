using ImogeIndex.Sources;

namespace ImogeIndex.Web2.Datamizes;

/// <summary>
/// ﾌｧｲﾙ供給元とRegistry
/// </summary>
public class FileSourcesController
{
    [Theory(DisplayName = "スキームの取り出し")]
    [InlineData(@"C:\images\a.png", null)]
    [InlineData(@"\\server\share\a.png", null)]
    [InlineData(@"relative\a.png", null)]
    [InlineData("sftp://host/a.png", "sftp")]
    [InlineData("mtp+usb://device/DCIM", "mtp+usb")]
    [InlineData("1abc://host", null)]
    [InlineData("c://host", null)]
    [InlineData("sf tp://host", null)]
    public void GetScheme(string location, string? expected)
    {
        Assert.Equal(expected, FileSourceRegistry.GetScheme(location));
    }

    [Fact(DisplayName = "スキーム無しはローカル、未登録スキームは読めない")]
    public void Resolve()
    {
        var mem = new InMemoryFileSource("mem");
        var registry = new FileSourceRegistry(LocalFileSource.Default, mem);
        Assert.Same(LocalFileSource.Default, registry.Resolve(@"C:\a.png"));
        Assert.Same(mem, registry.Resolve("MEM://box/a.png"));
        Assert.False(registry.TryResolve("sftp://host/a.png", out _));
        Assert.Throws<SourceUnavailableException>(() => registry.Resolve("sftp://host/a.png"));
        // 読み込み失敗(IOException)として扱われる
        Assert.IsAssignableFrom<IOException>(Record.Exception(() => registry.OpenRead("sftp://host/a.png")));
    }

    [Fact(DisplayName = "登録と解除")]
    public void Register()
    {
        var registry = new FileSourceRegistry(LocalFileSource.Default);
        var mem = new InMemoryFileSource("mem");
        using (registry.Register(mem)) {
            Assert.Same(mem, registry.Resolve("mem://box"));
            Assert.Throws<InvalidOperationException>(() => registry.Register(new InMemoryFileSource("MEM")));
        }
        Assert.False(registry.TryResolve("mem://box", out _));
    }

    [Fact(DisplayName = "比較は供給元に従う")]
    public void Comparer()
    {
        var registry = new FileSourceRegistry(LocalFileSource.Default, new InMemoryFileSource("mem"));
        var comparer = registry.Comparer;
        // ﾛーｶﾙは大文字小文字を無視
        Assert.True(comparer.Equals(@"C:\Images\a.png", @"c:\images\A.PNG"));
        Assert.Equal(comparer.GetHashCode(@"C:\Images\a.png"), comparer.GetHashCode(@"c:\images\A.PNG"));
        // memは区別する
        Assert.False(comparer.Equals("mem://box/a.png", "mem://box/A.png"));
        Assert.True(comparer.Equals("mem://box/a.png", "mem://box/a.png"));
        // 供給元が違えば別物
        Assert.False(comparer.Equals("mem://box/a.png", @"C:\box\a.png"));
        // 未登録ｽｷーﾑは区別する
        Assert.False(comparer.Equals("sftp://host/a.png", "sftp://host/A.png"));

        var set = new HashSet<string>(comparer) { @"C:\a.png", @"c:\A.png", "mem://box/a.png", "mem://box/A.png" };
        Assert.Equal(3, set.Count);
    }

    [Fact(DisplayName = "ローカルの場所の操作")]
    public void LocalLocation()
    {
        var local = LocalFileSource.Default;
        Assert.Equal(@"C:\scan\denied", local.Normalize(@"C:\scan\denied\"));
        Assert.Equal(@"D:\", local.Normalize(@"D:\"));
        Assert.Equal(@"C:\scan", local.GetParent(@"C:\scan\denied"));
        Assert.Null(local.GetParent(@"D:\"));
        Assert.True(local.IsSubdirectoryOrSame(@"c:\SCAN\denied\child", @"C:\scan\denied\"));
        Assert.True(local.IsSubdirectoryOrSame(@"C:\scan\denied", @"C:\scan\denied"));
        Assert.False(local.IsSubdirectoryOrSame(@"C:\scan\denied-other", @"C:\scan\denied"));
        Assert.False(local.IsSubdirectoryOrSame(@"C:\scan", @"C:\scan\denied"));
    }

    [Fact(DisplayName = "Registry経由で全部読める")]
    public void ReadAllBytes()
    {
        var mem = new InMemoryFileSource("mem");
        mem.Write("mem://box/a.bin", [1, 2, 3], DateTime.Now);
        var registry = new FileSourceRegistry(LocalFileSource.Default, mem);
        Assert.Equal([1, 2, 3], registry.ReadAllBytes("mem://box/a.bin"));
        Assert.Throws<FileNotFoundException>(() => registry.ReadAllBytes("mem://box/missing.bin"));
    }
}
