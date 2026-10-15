using ImogeIndex.Web6.Committees;
using ImogeIndex.Web6.Dictators;
using ImogeIndex.Mur;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;

namespace ImooneWPF.Committees;

/// <summary>
/// ﾘｽﾄのｶﾗﾑ制御(状態はFileListColumnStateが持ち、ここはGridViewColumnとの対応付けだけ)
/// </summary>
internal sealed class FileListColumnCommittee
{
    readonly FrameworkElement resourceOwner;
    readonly List<GridViewColumn> columns = new();
    FileListColumnState state = new([]);

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
        state = new FileListColumnState(columns.Select(column => column.Width).ToArray());
        state.Load(SettingsDictator.Default);
        ApplyVisibility(gridView);
        ApplySortTemplate(-1);
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
                IsChecked = state.IsVisible(index),
                IsEnabled = index != state.PinnedColumn,
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
        CaptureVisibleWidths();
        if (!state.SetVisibility(index, isVisible))
        {
            return false;
        }
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
        CaptureVisibleWidths();
        if (state.Pin(column == null ? -1 : columns.IndexOf(column)))
        {
            ApplyVisibility(gridView);
        }
    }

    /// <summary>
    /// ｶﾗﾑでｿーﾄ
    /// </summary>
    /// <param name="column"></param>
    /// <returns>並び順が変わった？</returns>
    public bool SortBy(GridViewColumn column)
    {
        var previous = state.SortIndicatorColumn;
        if (!state.SortBy(columns.IndexOf(column)))
        {
            return false;
        }
        ApplySortTemplate(previous);
        return true;
    }

    /// <summary>
    /// 現在のｿーﾄ状態の比較子
    /// </summary>
    /// <returns>ｿーﾄしないならnull</returns>
    public IComparer<FileItem>? CreateComparer() => state.CreateComparer();

    /// <summary>
    /// ｶﾗﾑの設定保存
    /// </summary>
    /// <param name="gridView"></param>
    public void CaptureSettings(GridView gridView)
    {
        CaptureVisibleWidths();
        state.UpdateVisibleOrder(gridView.Columns.Select(column => columns.IndexOf(column)).ToArray());
        state.Save(SettingsDictator.Default);
    }

    /// <summary>
    /// ｲﾝｼﾞｹーﾀー切り替え
    /// </summary>
    /// <param name="visible"></param>
    public void SetSortIndicatorVisible(bool visible)
    {
        var previous = state.SortIndicatorColumn;
        state.SortIndicatorHidden = !visible;
        ApplySortTemplate(previous);
    }

    /// <summary>
    /// 矢印
    /// </summary>
    /// <param name="previous">前に矢印を出していた列</param>
    private void ApplySortTemplate(int previous)
    {
        if (previous >= 0 && previous < columns.Count)
        {
            columns[previous].HeaderTemplate = null;
        }
        var current = state.SortIndicatorColumn;
        if (current < 0 || current >= columns.Count)
        {
            return;
        }
        columns[current].HeaderTemplate = (DataTemplate)resourceOwner.FindResource(
            state.SortDirection == ListSortDirection.Ascending
                ? "AscendingColumnHeaderTemplate"
                : "DescendingColumnHeaderTemplate");
    }

    /// <summary>
    /// ｶﾗﾑ幅取得
    /// </summary>
    private void CaptureVisibleWidths()
    {
        for (var index = 0; index < columns.Count; index++)
        {
            if (state.IsVisible(index))
            {
                state.SetWidth(index, columns[index].ActualWidth);
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
        foreach (var index in state.VisibleOrder)
        {
            columns[index].Width = state.GetWidth(index);
            gridView.Columns.Add(columns[index]);
        }
    }
}
