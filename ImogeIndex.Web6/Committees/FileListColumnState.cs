using ImogeIndex.Web6.Dictators;
using ImogeIndex.Mur;
using System.ComponentModel;

namespace ImogeIndex.Web6.Committees;

/// <summary>
/// 一覧の列の状態(幅/表示/並び順/ｿーﾄ)
/// </summary>
public sealed class FileListColumnState
{
    /// <summary>
    /// 列番号ごとのｿーﾄ対象
    /// ﾒﾀﾃﾞーﾀﾑ対応しようかと思ったけどﾒﾓﾘ食うのでやめた
    /// </summary>
    public static readonly IReadOnlyList<string> SortFields = [
        nameof(FileItem.Name),
        nameof(FileItem.Path),
        nameof(FileItem.DateTime),
        nameof(FileItem.Tag),
        nameof(FileItem.Character),
        nameof(FileItem.Keyword),
        nameof(FileItem.Costume),
        nameof(FileItem.Scene),
        nameof(FileItem.Objects),
        nameof(FileItem.Label),
    ];

    /// <summary>
    /// 列の表示文字列
    /// </summary>
    /// <param name="item"></param>
    /// <param name="index">列番号</param>
    /// <returns></returns>
    public static string? CellText(FileItem item, int index) => index switch {
        0 => item.Name,
        1 => item.Path,
        2 => item.DateTime.ToString("yyyy/MM/dd HH:mm:ss"),
        3 => item.Tag,
        4 => item.Character,
        5 => item.Keyword,
        6 => item.Costume,
        7 => item.Scene,
        8 => item.Objects,
        9 => item.Label,
        _ => null,
    };

    /// <summary>
    /// 列幅の下限
    /// </summary>
    public const double MinWidth = 10;

    readonly double[] widths;
    bool[] visibility;
    int[] order;

    /// <summary>
    /// ﾀー
    /// </summary>
    /// <param name="defaultWidths">列番号ごとの初期幅(列数もこれで決まる)</param>
    public FileListColumnState(IReadOnlyList<double> defaultWidths)
    {
        widths = defaultWidths.ToArray();
        visibility = Enumerable.Repeat(true, widths.Length).ToArray();
        order = Enumerable.Range(0, widths.Length).ToArray();
    }

    /// <summary>
    /// 列数
    /// </summary>
    public int Count => widths.Length;

    /// <summary>
    /// ｿーﾄ中の列(しないなら-1)
    /// </summary>
    public int SortColumn { get; private set; } = -1;

    /// <summary>
    /// ｿーﾄ方向
    /// </summary>
    public ListSortDirection SortDirection { get; private set; } = ListSortDirection.Ascending;

    /// <summary>
    /// ｿーﾄ矢印出さない状態(類似検索中とか)
    /// </summary>
    public bool SortIndicatorHidden { get; set; }

    /// <summary>
    /// ｿーﾄ矢印を出す列(出さないなら-1)
    /// </summary>
    public int SortIndicatorColumn => SortIndicatorHidden ? -1 : SortColumn;

    /// <summary>
    /// 非表示禁止の列(ｻﾑﾈ付きﾘｽﾄのﾌｧｲﾙ名列、無ければ-1)
    /// </summary>
    public int PinnedColumn { get; private set; } = -1;

    /// <summary>
    /// 列幅
    /// </summary>
    /// <param name="index"></param>
    /// <returns></returns>
    public double GetWidth(int index) => widths[index];

    /// <summary>
    /// 列幅更新(表示中の列の実幅を覚える用)
    /// </summary>
    /// <param name="index"></param>
    /// <param name="width">0以下は未確定とみなして無視</param>
    public void SetWidth(int index, double width)
    {
        if (width > 0) {
            widths[index] = Math.Max(MinWidth, width);
        }
    }

    /// <summary>
    /// 表示中？
    /// </summary>
    /// <param name="index"></param>
    /// <returns></returns>
    public bool IsVisible(int index) => visibility[index];

    /// <summary>
    /// 表示する列を並び順で
    /// </summary>
    public IEnumerable<int> VisibleOrder => order.Where(index => visibility[index]);

    /// <summary>
    /// 設定から復元
    /// </summary>
    /// <param name="settings"></param>
    public void Load(SettingsDictator settings)
    {
        if (settings.ColumnDimensions?.Length == Count) {
            for (var index = 0; index < Count; index++) {
                widths[index] = Math.Max(MinWidth, settings.ColumnDimensions[index]);
            }
        }
        visibility = settings.ColumnVisibility?.Length == Count
            ? settings.ColumnVisibility.ToArray()
            : Enumerable.Repeat(true, Count).ToArray();
        order = IsValidOrder(settings.ColumnOrder)
            ? settings.ColumnOrder!.ToArray()
            : Enumerable.Range(0, Count).ToArray();

        var sortColumn = settings.SortColumn;
        if (sortColumn < 0 || sortColumn >= SortFields.Count || sortColumn >= Count) {
            return;
        }
        SortColumn = sortColumn;
        SortDirection = settings.SortDescending
            ? ListSortDirection.Descending
            : ListSortDirection.Ascending;
    }

    /// <summary>
    /// 設定に書き出す
    /// </summary>
    /// <param name="settings"></param>
    public void Save(SettingsDictator settings)
    {
        settings.ColumnDimensions = widths.Select(width => Math.Max((int)MinWidth, (int)width)).ToArray();
        settings.ColumnVisibility = visibility.ToArray();
        settings.ColumnOrder = order.ToArray();
        settings.SortColumn = SortColumn;
        settings.SortDescending = SortDirection == ListSortDirection.Descending;
    }

    /// <summary>
    /// 表示切り替え
    /// </summary>
    /// <param name="index"></param>
    /// <param name="isVisible"></param>
    /// <returns>切り替えた？(最後の1列と非表示禁止の列は隠させない)</returns>
    public bool SetVisibility(int index, bool isVisible)
    {
        if (!isVisible && (visibility.Count(value => value) == 1 || index == PinnedColumn)) {
            return false;
        }
        visibility[index] = isVisible;
        return true;
    }

    /// <summary>
    /// 非表示禁止の列を設定(非表示中なら表示させる)
    /// </summary>
    /// <param name="index">-1で解除</param>
    /// <returns>表示状態が変わった？</returns>
    public bool Pin(int index)
    {
        PinnedColumn = index;
        if (index < 0 || index >= Count || visibility[index]) {
            return false;
        }
        visibility[index] = true;
        return true;
    }

    /// <summary>
    /// 列でｿーﾄ(同じ列なら向きを反転)
    /// </summary>
    /// <param name="index"></param>
    /// <returns>並び順が変わった？</returns>
    public bool SortBy(int index)
    {
        if (index < 0 || index >= SortFields.Count || index >= Count) {
            return false;
        }

        if (SortColumn == index) {
            SortDirection = SortDirection == ListSortDirection.Ascending
                ? ListSortDirection.Descending
                : ListSortDirection.Ascending;
        }
        else {
            SortColumn = index;
            SortDirection = ListSortDirection.Ascending;
        }
        return true;
    }

    /// <summary>
    /// 現在のｿーﾄ状態の比較子
    /// ﾘﾌﾚｸｼｮﾝ経由のSortDescriptionsは重いので型付きで比べる
    /// </summary>
    /// <returns>ｿーﾄしないならnull</returns>
    public IComparer<FileItem>? CreateComparer()
    {
        if (SortColumn < 0 || SortColumn >= SortFields.Count) {
            return null;
        }
        return new FileItemComparer(SortFields[SortColumn], SortDirection == ListSortDirection.Descending);
    }

    /// <summary>
    /// ﾕーｻﾞーが並べ替えた後の表示中の列順を反映する
    /// </summary>
    /// <param name="visibleOrder">表示中の列番号を画面の並び順で</param>
    public void UpdateVisibleOrder(IReadOnlyList<int> visibleOrder)
    {
        if (order.Length != Count) {
            return;
        }
        if (visibleOrder.Count != visibility.Count(value => value) || visibleOrder.Any(index => index < 0 || index >= Count)) {
            return;
        }
        var visibleIndex = 0;
        for (var index = 0; index < order.Length; index++) {
            if (visibility[order[index]]) {
                order[index] = visibleOrder[visibleIndex++];
            }
        }
    }

    /// <summary>
    /// この並び順有効？
    /// </summary>
    /// <param name="candidate"></param>
    /// <returns></returns>
    private bool IsValidOrder(int[]? candidate)
    {
        return candidate?.Length == Count &&
            candidate.All(index => index >= 0 && index < Count) &&
            candidate.Distinct().Count() == Count;
    }
}

/// <summary>
/// 一覧のｿーﾄ用比較子
/// </summary>
public sealed class FileItemComparer : IComparer<FileItem>
{
    readonly Comparison<FileItem> comparison;
    readonly bool descending;

    public FileItemComparer(string field, bool descending)
    {
        this.descending = descending;
        comparison = field switch {
            nameof(FileItem.Path) => static (a, b) => {
                var c = CompareText(a.Path, b.Path);
                return c != 0 ? c : CompareText(a.Name, b.Name);
            },
            nameof(FileItem.DateTime) => static (a, b) => a.DateTime.CompareTo(b.DateTime),
            nameof(FileItem.Tag) => static (a, b) => CompareText(a.Tag, b.Tag),
            nameof(FileItem.Character) => static (a, b) => CompareText(a.Character, b.Character),
            nameof(FileItem.Keyword) => static (a, b) => CompareText(a.Keyword, b.Keyword),
            nameof(FileItem.Costume) => static (a, b) => CompareText(a.Costume, b.Costume),
            nameof(FileItem.Scene) => static (a, b) => CompareText(a.Scene, b.Scene),
            nameof(FileItem.Objects) => static (a, b) => CompareText(a.Objects, b.Objects),
            nameof(FileItem.Label) => static (a, b) => CompareText(a.Label, b.Label),
            _ => static (a, b) => CompareText(a.Name, b.Name),
        };
    }

    public int Compare(FileItem? x, FileItem? y)
    {
        if (ReferenceEquals(x, y)) {
            return 0;
        }
        if (x is null) {
            return descending ? 1 : -1;
        }
        if (y is null) {
            return descending ? -1 : 1;
        }
        var c = comparison(x, y);
        return descending ? -c : c;
    }

    /// <summary>
    /// ｶﾙﾁｬー指定比較は遅いので大文字小文字無視のOrdinalで
    /// </summary>
    /// <param name="a"></param>
    /// <param name="b"></param>
    /// <returns></returns>
    private static int CompareText(string? a, string? b)
        => string.Compare(a, b, StringComparison.OrdinalIgnoreCase);
}
