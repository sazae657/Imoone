using ImogeIndex.Web6.Committees;
using ImogeIndex.Web6.Dictators;
using ImogeIndex.Web6.Modelle;
using ImooneWPF.Controls;
using ImooneWPF.Dictators;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace ImooneWPF.Committees;

/// <summary>
/// 一覧の表示方式とﾀｲﾙのｷー操作を統治する
/// </summary>
public sealed class FileListDisplayCommittee : IDisposable {
    readonly EssenUndFressenView list;
    readonly MenuItem[] modeEitems;
    readonly GridView columns;
    readonly ItemsPanelTemplate originalPanel;
    readonly GridViewColumn nameColumn;
    readonly BindingBase nameBinding;
    readonly Action captureColumnsDelegaty;
    readonly Action<FileListSizementEnvuelve> saveZoomDelegaty;
    readonly TileThumbnailDictator<BitmapSource> thumbnails = new(WpfTileBitmapDecoder.Instance);
    readonly WheelNotchAccumulator wheel = new();
    int selectionAnchor = -1;
    /// <summary>
    /// 表示中の拡大率
    /// </summary>
    FileListSizementEnvuelve zoom = new(MurDisplayMode.List, FileListSizementEnvuelve.MinIconSize);

    /// <summary>
    /// 要求拡大率(最大寸法で切られる前)
    /// </summary>
    FileListSizementEnvuelve requested = new(MurDisplayMode.List, FileListSizementEnvuelve.MinIconSize);

    int maxIconSize = FileListSizementEnvuelve.IconSizeCeiling;
    int changementVer;
    string? datumPath;
    bool disposedValue;

    /// <summary>
    /// </summary>
    /// <param name="flv">一覧</param>
    /// <param name="modeMenu">表示方式ﾒﾆｭー</param>
    /// <param name="fileNameColumn">ﾌｧｲﾙ名列</param>
    /// <param name="captureColumns">列の状態を控える</param>
    /// <param name="initialZoom">最初の拡大率</param>
    /// <param name="saveZoom">拡大率が変わった(最大寸法で切られる前のが来る)</param>
    public FileListDisplayCommittee(EssenUndFressenView flv, MenuItem modeMenu, GridViewColumn fileNameColumn, Action captureColumns,
        FileListSizementEnvuelve initialZoom, Action<FileListSizementEnvuelve> saveZoom) {
        list = flv;
        nameColumn = fileNameColumn;
        nameBinding = fileNameColumn.DisplayMemberBinding;
        modeEitems = modeMenu.Items.Cast<MenuItem>().ToArray();
        captureColumnsDelegaty = captureColumns;
        saveZoomDelegaty = saveZoom;
        columns = (GridView)list.View;
        originalPanel = list.ItemsPanel;
        Rubbernail.SetLoader(list, thumbnails);
        list.PreviewKeyDown += OnKeyDown;
        list.PreviewMouseLeftButtonDown += OnMouseDown;
        list.PreviewMouseWheel += OnMouseWheel;
        foreach (var item in modeEitems) {
            item.Click += OnModeClicked;
        }
        SetZoom(initialZoom.Clamp(maxIconSize));
    }

    /// <summary>
    /// ｻﾑﾈﾃﾞーﾀﾑ設置
    /// </summary>
    /// <param name="path"></param>
    public void SetSmallDatum(string? path) {
        if (disposedValue) {
            return;
        }
        datumPath = path;
        thumbnails.Reset(UsesThumbnail(zoom.Mode) ? datumPath : null);
    }

    /// <summary>
    /// ｱｲｺﾝの最大寸法設定(ﾌﾟﾛﾌｧｲﾙ)
    /// </summary>
    /// <param name="size"></param>
    public void SetMaxIconSize(int size) {
        if (disposedValue) {
            return;
        }
        maxIconSize = size;
        Apply(requested.Clamp(maxIconSize));
    }

    /// <summary>
    /// GridViewで表示するﾓーﾄﾞ？
    /// </summary>
    /// <param name="target"></param>
    /// <returns></returns>
    private static bool IsGridMode(MurDisplayMode target)
        => target is MurDisplayMode.List or MurDisplayMode.ThumbList;

    /// <summary>
    /// ｻﾑﾈを読むﾓーﾄﾞ？
    /// </summary>
    /// <param name="target"></param>
    /// <returns></returns>
    private static bool UsesThumbnail(MurDisplayMode target) => target != MurDisplayMode.List;

    /// <summary>
    /// ﾌｧｲﾙ名列のｾﾙ切替
    /// DisplayMemberBindingがあるとCellTemplateが無視されるので付け外しする
    /// </summary>
    /// <param name="withThumbnail"></param>
    private void ApplyNameCell(bool withThumbnail) {
        if (withThumbnail) {
            nameColumn.DisplayMemberBinding = null;
            nameColumn.CellTemplate = (DataTemplate)list.FindResource("ListThumbCellTemplate");
        }
        else {
            nameColumn.CellTemplate = null;
            nameColumn.DisplayMemberBinding = nameBinding;
        }
    }

    /// <summary>
    /// 表示ﾓーﾄﾞﾒﾆｭーﾊﾝﾄﾞﾗ
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="args"></param>
    private void OnModeClicked(object sender, RoutedEventArgs args) {
        if (sender is not MenuItem { Tag: MurDisplayPreset preset }) {
            return;
        }
        SetPreset(preset);
        args.Handled = true;
    }

    /// <summary>
    /// Ctrl+ﾎｲーﾙで拡大縮小
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="args"></param>
    private void OnMouseWheel(object sender, MouseWheelEventArgs args) {
        if (!Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) {
            wheel.Reset();
            return;
        }
        // 拡大縮小できない端でも一覧はｽｸﾛーﾙさせない
        args.Handled = true;
        var steps = wheel.Add(args.Delta);
        if (steps != 0) {
            SetZoom(zoom.Step(steps, maxIconSize));
        }
    }

    /// <summary>
    /// ﾌﾟﾘｾｯﾄに切替
    /// </summary>
    /// <param name="preset"></param>
    public void SetPreset(MurDisplayPreset preset) {
        if (!Enum.IsDefined(preset)) {
            return;
        }
        SetZoom(FileListSizementEnvuelve.FromPreset(preset, maxIconSize));
    }

    /// <summary>
    /// 操作で拡大率を変える
    /// </summary>
    /// <param name="next"></param>
    private void SetZoom(FileListSizementEnvuelve next) {
        if (disposedValue) {
            return;
        }
        var changed = next != requested;
        requested = next;
        Apply(next);
        if (changed) {
            saveZoomDelegaty(requested);
        }
    }

    /// <summary>
    /// 拡大率を見た目に反映
    /// </summary>
    /// <param name="next"></param>
    private void Apply(FileListSizementEnvuelve next) {
        if (disposedValue) {
            return;
        }
        var matched = next.MatchPreset(maxIconSize);
        foreach (var item in modeEitems) {
            item.IsChecked = item.Tag is MurDisplayPreset itemPreset && itemPreset == matched;
        }
        if (next == zoom) {
            return;
        }

        var selected = list.SelectedItem;
        var selectedItems = list.SelectedItems.Cast<object>().ToArray();

        var scroll = FindChild<ScrollViewer>(list);
        var anchor = FindVisibleItem(scroll) ?? selected;
        var hadFocus = list.IsKeyboardFocusWithin;
        var version = ++changementVer;
        var previous = zoom;
        if (IsGridMode(previous.Mode) && previous.Mode != next.Mode) {
            captureColumnsDelegaty();
        }
        zoom = next;
        if (UsesThumbnail(previous.Mode) != UsesThumbnail(zoom.Mode)) {
            // 寸法はｷｬｯｼｭのｷーに入っているので、拡大縮小だけなら捨てない
            thumbnails.Reset(UsesThumbnail(zoom.Mode) ? datumPath : null);
        }
        ApplyNameCell(zoom.Mode == MurDisplayMode.ThumbList);
        if (IsGridMode(zoom.Mode)) {
            list.ItemTemplate = null;
            list.ItemsPanel = originalPanel;
            list.View = columns;
            ScrollViewer.SetHorizontalScrollBarVisibility(list, ScrollBarVisibility.Auto);
        }
        else {
            TileMetrics.SetIconSize(list, zoom.IconSize);
            TileMetrics.SetItemWidth(list, zoom.TileWidth);
            TileMetrics.SetItemHeight(list, zoom.TileHeight);
            list.View = null;
            list.ItemTemplate = (DataTemplate)list.FindResource(zoom.Horizontal ? "TileTemplateHorizontal" : "TileTemplateVertical");
            list.ItemsPanel = (ItemsPanelTemplate)list.FindResource("TilePanel");
            ScrollViewer.SetHorizontalScrollBarVisibility(list, ScrollBarVisibility.Disabled);
        }
        if (!list.SelectedItems.Cast<object>().SequenceEqual(selectedItems)) {
            list.SetSelection(selectedItems);
        }
        selectionAnchor = -1;
        list.Dispatcher.BeginInvoke(() => {
            if (version != changementVer) {
                return;
            }
            if (anchor != null && list.Items.Contains(anchor)) {
                list.ScrollIntoView(anchor);
            }
            if (hadFocus && selected != null) {
                (list.ItemContainerGenerator.ContainerFromItem(selected) as ListViewItem)?.Focus();
            }
        }, DispatcherPriority.Loaded);
    }

    /// <summary>
    /// 見えてるやつ探し
    /// </summary>
    /// <param name="scroll"></param>
    /// <returns></returns>
    private object? FindVisibleItem(ScrollViewer? scroll) {
        if (scroll == null) {
            return null;
        }
        var panel = FindChild<System.Windows.Controls.Panel>(FindChild<ItemsPresenter>(list));
        if (panel == null) {
            return null;
        }
        foreach (var c in panel.Children.OfType<ListViewItem>()) {
            var bounds = c.TransformToAncestor(scroll).TransformBounds(new Rect(c.RenderSize));
            if (bounds.Bottom > 0 && bounds.Top < scroll.ActualHeight) {
                return c.Content;
            }
        }
        return null;
    }

    /// <summary>
    /// ｱｲｺﾝﾓーﾄﾞの時のｷーﾎﾞーﾄﾞ選択ﾊﾝﾄﾞﾘﾝｸﾞ
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="args"></param>
    private void OnKeyDown(object sender, KeyEventArgs args) {
        if (IsGridMode(zoom.Mode) || list.Items.Count == 0 || Keyboard.Modifiers.HasFlag(ModifierKeys.Alt)) {
            return;
        }
        var panel = FindChild<EssenUndFressenPanel>(list);
        if (panel == null) {
            return;
        }
        var focused = Keyboard.FocusedElement as DependencyObject;
        while (focused != null && focused is not ListViewItem) {
            focused = VisualTreeHelper.GetParent(focused);
        }
        TileMove? move = args.Key switch
        {
            Key.Left => TileMove.Left,
            Key.Right => TileMove.Right,
            Key.Up => TileMove.Up,
            Key.Down => TileMove.Down,
            Key.Home => TileMove.Home,
            Key.End => TileMove.End,
            Key.PageUp => TileMove.PageUp,
            Key.PageDown => TileMove.PageDown,
            _ => null
        };
        if (move == null) {
            return;
        }
        var current = focused == null ? list.SelectedIndex : list.ItemContainerGenerator.IndexFromContainer(focused);
        var page = Math.Max(1, (int)(panel.ViewportHeight / panel.ItemHeight)) * panel.Columns;
        var next = TileNavigation.Next(move.Value, current, panel.Columns, page, list.Items.Count);
        current = Math.Max(0, current);
        var shift = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);
        var control = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);
        if (!shift && !control) {
            selectionAnchor = next;
        }
        if (shift && list.SelectionMode != SelectionMode.Single) {
            // TODO: 複数選択対応したが、そもそも複数選択して何すんだという考えに至ったので残しとくけどたぶん使わない
            if (selectionAnchor < 0) {
                selectionAnchor = current;
            }
            selectionAnchor = Math.Clamp(selectionAnchor, 0, list.Items.Count - 1);
            var start = Math.Min(selectionAnchor, next);
            var range = Enumerable.Range(start, Math.Abs(selectionAnchor - next) + 1).Select(i => list.Items[i]);
            list.SetSelection(control ? list.SelectedItems.Cast<object>().Union(range).ToArray() : range.ToArray());
        }
        else if (!control) {
            list.SelectedItem = list.Items[next];
        }
        panel.Reveal(next);
        // 同期ﾚｲｱｳﾄは重いので、移動先のｺﾝﾃﾅがまだ無い(画面外へ飛んだ)時だけ強制する
        var target = list.ItemContainerGenerator.ContainerFromIndex(next);
        if (target == null) {
            list.UpdateLayout();
            target = list.ItemContainerGenerator.ContainerFromIndex(next);
        }
        if (target is ListViewItem container) {
            var prev = list.SelectedItems.Cast<object>().ToArray();
            container.Focus();
            if (shift || control) {
                if (!list.SelectedItems.Cast<object>().SequenceEqual(prev)) {
                    list.SetSelection(prev);
                }
            }
        }
        args.Handled = true;
    }

    private void OnMouseDown(object sender, MouseButtonEventArgs args) => selectionAnchor = -1;

    private static T? FindChild<T>(DependencyObject? parent) where T : DependencyObject {
        if (parent == null) {
            return null;
        }
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++) {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T result) {
                return result;
            }
            var nested = FindChild<T>(child);
            if (nested != null) {
                return nested;
            }
        }
        return null;
    }

    #region IDisposable

    private void Dispose(bool disposing) {
        if (disposedValue) {
            return;
        }
        disposedValue = true;
        changementVer++;
        list.PreviewKeyDown -= OnKeyDown;
        list.PreviewMouseLeftButtonDown -= OnMouseDown;
        list.PreviewMouseWheel -= OnMouseWheel;
        foreach (var item in modeEitems) {
            item.Click -= OnModeClicked;
        }
        Rubbernail.SetLoader(list, null);
        thumbnails.Dispose();
    }

    public void Dispose() {
        // このｺーﾄﾞを変更しないでください。ｸﾘーﾝｱｯﾌﾟ ｺーﾄﾞを 'Dispose(bool disposing)' ﾒｿｯﾄﾞに記述します
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
    #endregion
}

