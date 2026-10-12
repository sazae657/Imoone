using ImooneWPF.Dictators;
using ImogeIndex.Mur;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;

namespace ImooneWPF.Committees;

/// <summary>
/// ﾘｽﾄのｶﾗﾑ制御
/// </summary>
internal sealed class FileListColumnCommittee
{
    readonly FrameworkElement resourceOwner;
    readonly List<GridViewColumn> columns = new();
    readonly List<double> widths = new();

    // ﾒﾀﾃﾞーﾀﾑ対応しようかと思ったけどﾒﾓﾘ食うのでやめ
    readonly string[] sortFields = [
        nameof(FileItem.Name),
        nameof(FileItem.Path),
        nameof(FileItem.DateTime),
        nameof(FileItem.Tag),
        nameof(FileItem.Character),
        nameof(FileItem.Keyword),
        nameof(FileItem.Costume),
        nameof(FileItem.Scene),
        nameof(FileItem.Objects)
    ];

    bool[] visibility = Array.Empty<bool>();
    int[] order = Array.Empty<int>();
    GridViewColumn? sortColumn;
    ListSortDirection sortDirection = ListSortDirection.Ascending;
    // ｿーﾄ矢印出さない状態(類似検索中とか)
    bool sortIndicatorHidden;
    // 非表示禁止の列(ｻﾑﾈ付きﾘｽﾄのﾌｧｲﾙ名列)
    GridViewColumn? pinnedColumn;

    public FileListColumnCommittee(FrameworkElement owner)
    {
        resourceOwner = owner;
    }

    /// <summary>
    /// それある？
    /// </summary>
    /// <param name="column"></param>
    /// <returns></returns>
    public bool Contains(GridViewColumn? column) => column != null && columns.Contains(column);

    /// <summary>
    /// ﾀ
    /// </summary>
    /// <param name="gridView"></param>
    public void Initialize(GridView gridView)
    {
        if (columns.Count != 0)
        {
            return;
        }

        columns.AddRange(gridView.Columns);
        widths.AddRange(columns.Select(column => column.Width));
        if (SettingsDictator.Default.ColumnDimensions?.Length == columns.Count)
        {
            for (var index = 0; index < columns.Count; index++)
            {
                widths[index] = Math.Max(10, SettingsDictator.Default.ColumnDimensions[index]);
            }
        }
        visibility = SettingsDictator.Default.ColumnVisibility?.Length == columns.Count
            ? SettingsDictator.Default.ColumnVisibility.ToArray()
            : Enumerable.Repeat(true, columns.Count).ToArray();
        order = IsValidOrder(SettingsDictator.Default.ColumnOrder)
            ? SettingsDictator.Default.ColumnOrder!.ToArray()
            : Enumerable.Range(0, columns.Count).ToArray();
        ApplyVisibility(gridView);
        RestoreSort();
    }

    /// <summary>
    /// ﾍｯﾀﾞーの右ｸﾘﾒﾆｭー作成
    /// </summary>
    /// <param name="header"></param>
    /// <param name="clickHandler"></param>
    /// <returns></returns>
    public ContextMenu CreateVisibilityMenu(GridViewColumnHeader header, RoutedEventHandler clickHandler)
    {
        var contextMenu = new ContextMenu();
        for (var index = 0; index < columns.Count; index++)
        {
            var menuItem = new MenuItem
            {
                Header = columns[index].Header,
                IsCheckable = true,
                IsChecked = visibility[index],
                IsEnabled = columns[index] != pinnedColumn,
                Tag = index
            };
            menuItem.Click += clickHandler;
            contextMenu.Items.Add(menuItem);
        }
        contextMenu.PlacementTarget = header;
        return contextMenu;
    }

    /// <summary>
    /// 表示切り替え
    /// </summary>
    /// <param name="gridView"></param>
    /// <param name="index"></param>
    /// <param name="isVisible"></param>
    /// <returns></returns>
    public bool SetVisibility(GridView gridView, int index, bool isVisible)
    {
        if (!isVisible && (visibility.Count(value => value) == 1 || columns[index] == pinnedColumn))
        {
            return false;
        }
        CaptureVisibleWidths();
        visibility[index] = isVisible;
        ApplyVisibility(gridView);
        return true;
    }

    /// <summary>
    /// 非表示禁止の列を設定(非表示中なら表示させる)
    /// </summary>
    /// <param name="gridView"></param>
    /// <param name="column">nullで解除</param>
    public void Discipline(GridView gridView, GridViewColumn? column)
    {
        pinnedColumn = column;
        var index = column == null ? -1 : columns.IndexOf(column);
        if (index < 0 || visibility[index])
        {
            return;
        }
        CaptureVisibleWidths();
        visibility[index] = true;
        ApplyVisibility(gridView);
    }

    /// <summary>
    /// ｶﾗﾑでｿーﾄ
    /// </summary>
    /// <param name="column"></param>
    /// <returns>並び順が変わった？</returns>
    public bool SortBy(GridViewColumn column)
    {
        var columnIndex = columns.IndexOf(column);
        if (columnIndex < 0 || columnIndex >= sortFields.Length)
        {
            return false;
        }

        if (sortColumn == column)
        {
            sortDirection = sortDirection == ListSortDirection.Ascending
                ? ListSortDirection.Descending
                : ListSortDirection.Ascending;
        }
        else
        {
            if (sortColumn != null)
            {
                sortColumn.HeaderTemplate = null;
            }
            sortColumn = column;
            sortDirection = ListSortDirection.Ascending;
        }
        ApplySortTemplate();
        return true;
    }

    /// <summary>
    /// 現在のｿーﾄ状態の比較子
    /// ﾘﾌﾚｸｼｮﾝ経由のSortDescriptionsは重いので型付きで比べる
    /// </summary>
    /// <returns>ｿーﾄしないならnull</returns>
    public IComparer<FileItem>? CreateComparer()
    {
        if (sortColumn == null)
        {
            return null;
        }
        var columnIndex = columns.IndexOf(sortColumn);
        if (columnIndex < 0 || columnIndex >= sortFields.Length)
        {
            return null;
        }
        return new FileItemComparer(sortFields[columnIndex], sortDirection == ListSortDirection.Descending);
    }

    /// <summary>
    /// ｶﾗﾑの設定保存
    /// </summary>
    /// <param name="gridView"></param>
    public void CaptureSettings(GridView gridView)
    {
        CaptureVisibleWidths();
        UpdateOrder(gridView);
        SettingsDictator.Default.ColumnDimensions = widths.Select(width => Math.Max(10, (int)width)).ToArray();
        SettingsDictator.Default.ColumnVisibility = visibility.ToArray();
        SettingsDictator.Default.ColumnOrder = order.ToArray();
        SettingsDictator.Default.SortColumn = sortColumn == null ? -1 : columns.IndexOf(sortColumn);
        SettingsDictator.Default.SortDescending = sortDirection == ListSortDirection.Descending;
    }

    /// <summary>
    /// ｿーﾄ状態復元
    /// </summary>
    private void RestoreSort()
    {
        var columnIndex = SettingsDictator.Default.SortColumn;
        if (columnIndex < 0 || columnIndex >= sortFields.Length || columnIndex >= columns.Count)
        {
            return;
        }
        sortColumn = columns[columnIndex];
        sortDirection = SettingsDictator.Default.SortDescending
            ? ListSortDirection.Descending
            : ListSortDirection.Ascending;
        ApplySortTemplate();
    }

    /// <summary>
    /// ｲﾝｼﾞｹーﾀー切り替え
    /// </summary>
    /// <param name="visible"></param>
    public void SetSortIndicatorVisible(bool visible)
    {
        sortIndicatorHidden = !visible;
        ApplySortTemplate();
    }


    /// <summary>
    /// 矢印
    /// </summary>
    private void ApplySortTemplate()
    {
        if (sortColumn == null)
        {
            return;
        }
        if (sortIndicatorHidden) {
            sortColumn.HeaderTemplate = null;
            return;
        }
        sortColumn.HeaderTemplate = (DataTemplate)resourceOwner.FindResource(
            sortDirection == ListSortDirection.Ascending
                ? "AscendingColumnHeaderTemplate"
                : "DescendingColumnHeaderTemplate");
    }

    /// <summary>
    /// このｿーﾄ有効？
    /// </summary>
    /// <param name="candidate"></param>
    /// <returns></returns>
    private bool IsValidOrder(int[]? candidate)
    {
        return candidate?.Length == columns.Count &&
            candidate.All(index => index >= 0 && index < columns.Count) &&
            candidate.Distinct().Count() == columns.Count;
    }

    /// <summary>
    /// ｶﾗﾑ幅取得
    /// </summary>
    private void CaptureVisibleWidths()
    {
        for (var index = 0; index < columns.Count; index++)
        {
            if (visibility[index] && columns[index].ActualWidth > 0)
            {
                widths[index] = Math.Max(10, columns[index].ActualWidth);
            }
        }
    }

    /// <summary>
    /// 表示/非表示適用
    /// </summary>
    /// <param name="gridView"></param>
    private void ApplyVisibility(GridView gridView)
    {
        gridView.Columns.Clear();
        foreach (var index in order)
        {
            if (!visibility[index])
            {
                continue;
            }
            columns[index].Width = widths[index];
            gridView.Columns.Add(columns[index]);
        }
    }

    /// <summary>
    /// order更新
    /// </summary>
    /// <param name="gridView"></param>
    private void UpdateOrder(GridView gridView)
    {
        if (order.Length != columns.Count)
        {
            return;
        }
        var visibleOrder = gridView.Columns.Select(column => columns.IndexOf(column)).ToArray();
        if (visibleOrder.Length != visibility.Count(value => value) || visibleOrder.Any(index => index < 0))
        {
            return;
        }
        var visibleIndex = 0;
        for (var index = 0; index < order.Length; index++)
        {
            if (visibility[order[index]])
            {
                order[index] = visibleOrder[visibleIndex++];
            }
        }
    }
}

/// <summary>
/// 一覧のｿーﾄ用比較子
/// </summary>
internal sealed class FileItemComparer : IComparer<FileItem>
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
