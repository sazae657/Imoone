using CommunityToolkit.Mvvm.ComponentModel;

namespace ImogeIndex.Web6.Modelle;

/// <summary>
/// ﾌｫﾙﾀﾞーﾂﾘーの節
/// 最上位(ﾌﾟﾛﾌｧｲﾙ名)だけLocationがnull
/// </summary>
public sealed partial class FolderNode : ObservableObject
{
    internal readonly List<FolderNode> children = new();

    internal FolderNode(string name, string? location, FolderNode? parent)
    {
        Name = name;
        Location = location;
        Parent = parent;
    }

    /// <summary>
    /// 表示名
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// ﾌｫﾙﾀﾞーの場所(最上位はnull)
    /// </summary>
    public string? Location { get; }

    /// <summary>
    /// 親(最上位はnull)
    /// </summary>
    public FolderNode? Parent { get; }

    /// <summary>
    /// 子
    /// </summary>
    public IReadOnlyList<FolderNode> Children => children;

    /// <summary>
    /// 最上位(ﾌﾟﾛﾌｧｲﾙ名)？
    /// </summary>
    public bool IsProfileRoot => Parent == null;

    /// <summary>
    /// 配下の全件数
    /// </summary>
    public int TotalCount { get; internal set; }

    /// <summary>
    /// 深さ優先の先行順の番号
    /// </summary>
    internal int Order { get; set; }

    /// <summary>
    /// 配下の番号の終わり(Order以上End未満が部分木)
    /// </summary>
    internal int End { get; set; }

    /// <summary>
    /// 現在の検索結果での配下件数
    /// </summary>
    [ObservableProperty]
    public partial int HitCount { get; set; }

    /// <summary>
    /// 表示する？
    /// </summary>
    [ObservableProperty]
    public partial bool IsVisible { get; set; } = true;

    /// <summary>
    /// 展開してる？
    /// </summary>
    [ObservableProperty]
    public partial bool IsExpanded { get; set; }

    /// <summary>
    /// 選択されてる？
    /// </summary>
    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    public override string ToString() => Location ?? Name;
}
