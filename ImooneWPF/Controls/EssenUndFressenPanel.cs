using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace ImooneWPF.Controls;

/// <summary>
/// ｱｲｺﾝﾀｲﾘﾝｸﾞ用ぱねる
/// </summary>
public sealed class EssenUndFressenPanel : VirtualizingPanel, IScrollInfo
{
    public static readonly DependencyProperty ItemWidthProperty = DependencyProperty.Register(
        nameof(ItemWidth), typeof(double), typeof(EssenUndFressenPanel),
        new FrameworkPropertyMetadata(128.0, FrameworkPropertyMetadataOptions.AffectsMeasure),
        value => value is double number && double.IsFinite(number) && number > 0);
    public static readonly DependencyProperty ItemHeightProperty = DependencyProperty.Register(
        nameof(ItemHeight), typeof(double), typeof(EssenUndFressenPanel),
        new FrameworkPropertyMetadata(80.0, FrameworkPropertyMetadataOptions.AffectsMeasure),
        value => value is double number && double.IsFinite(number) && number > 0);

    public double ItemWidth {
        get => (double)GetValue(ItemWidthProperty);
        set => SetValue(ItemWidthProperty, value);
    }

    public double ItemHeight {
        get => (double)GetValue(ItemHeightProperty);
        set => SetValue(ItemHeightProperty, value);
    }

    public int Columns { get; private set; } = 1;

    double cellWidth;

    protected override Size MeasureOverride(Size availableSize)
    {
        var owner = ItemsControl.GetItemsOwner(this);
        var count = owner?.Items.Count ?? 0;
        var width = double.IsFinite(availableSize.Width) ? Math.Max(0, availableSize.Width) : ItemWidth;
        var height = double.IsFinite(availableSize.Height) ? Math.Max(0, availableSize.Height) : ItemHeight;
        Columns = Math.Max(1, (int)(width / ItemWidth));
        cellWidth = Math.Min(ItemWidth, width);
        ViewportWidth = width;
        ViewportHeight = height;
        ExtentWidth = width;
        ExtentHeight = Math.Ceiling((double)count / Columns) * ItemHeight;
        VerticalOffset = Math.Clamp(VerticalOffset, 0, Math.Max(0, ExtentHeight - height));
        ScrollOwner?.InvalidateScrollInfo();

        var first = Math.Max(0, ((int)(VerticalOffset / ItemHeight) - 1) * Columns);
        var last = Math.Min(count - 1, ((int)Math.Ceiling((VerticalOffset + height) / ItemHeight) + 1) * Columns - 1);

        var children = InternalChildren;
        var zenerator = ItemContainerGenerator;

        if (children.Count > 0 && zenerator.IndexFromGeneratorPosition(new GeneratorPosition(children.Count - 1, 0)) < 0)
        {
            // 子要素とｼﾞｪﾈﾚーﾀの対応が崩れているので作り直し
            zenerator.RemoveAll();
            RemoveInternalChildRange(0, children.Count);
        }

        // 画面外のやつは画像要求を解除して、Recyclingなら使い回しに回す。そうでなければ破棄
        var recycling = owner != null && GetVirtualizationMode(owner) == VirtualizationMode.Recycling;
        for (var i = children.Count - 1; i >= 0; i--)
        {
            var position = new GeneratorPosition(i, 0);
            var index = zenerator.IndexFromGeneratorPosition(position);
            if (index >= first && index <= last)
            {
                continue;
            }
            if (recycling)
            {
                ((IRecyclingItemContainerGenerator)zenerator).Recycle(position, 1);
            }
            else
            {
                zenerator.Remove(position, 1);
            }
            RemoveInternalChildRange(i, 1);
        }

        if (last >= first)
        {
            var start = zenerator.GeneratorPositionFromIndex(first);
            var childIndex = start.Offset == 0 ? start.Index : start.Index + 1;
            using (zenerator.StartAt(start, GeneratorDirection.Forward, true))
            {
                for (var index = first; index <= last; index++, childIndex++)
                {
                    var child = (UIElement)zenerator.GenerateNext(out var newlyRealized);
                    if (newlyRealized || VisualTreeHelper.GetParent(child) != this)
                    {
                        InsertInternalChild(childIndex, child);
                        zenerator.PrepareItemContainer(child);
                    }
                    child.Measure(new Size(cellWidth, ItemHeight));
                }
            }
        }
        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        for (var i = 0; i < InternalChildren.Count; i++)
        {
            var index = ItemContainerGenerator.IndexFromGeneratorPosition(new GeneratorPosition(i, 0));
            InternalChildren[i].Arrange(new Rect(index % Columns * cellWidth,
                index / Columns * ItemHeight - VerticalOffset, cellWidth, ItemHeight));
        }
        return finalSize;
    }

    protected override void OnItemsChanged(object sender, ItemsChangedEventArgs args)
    {
        base.OnItemsChanged(sender, args);
        ItemContainerGenerator.RemoveAll();
        RemoveInternalChildRange(0, InternalChildren.Count);
        InvalidateMeasure();
    }

    protected override void BringIndexIntoView(int index) => Reveal(index);

    public void Reveal(int index)
    {
        if (index < 0) {
            return;
        }
        var top = index / Columns * ItemHeight;
        if (top < VerticalOffset) {
            SetVerticalOffset(top);
        }
        else if (top + ItemHeight > VerticalOffset + ViewportHeight) {
            SetVerticalOffset(top + ItemHeight - ViewportHeight);
        }
    }

    #region IScrollInfo

    public Rect MakeVisible(Visual visual, Rect rectangle)
    {
        DependencyObject? child = visual;
        while (child != null && VisualTreeHelper.GetParent(child) != this)
            child = VisualTreeHelper.GetParent(child);
        if (child is UIElement element)
        {
            var position = InternalChildren.IndexOf(element);
            if (position >= 0)
            {
                var index = ItemContainerGenerator.IndexFromGeneratorPosition(new GeneratorPosition(position, 0));
                Reveal(index);
                return new Rect(index % Columns * cellWidth, index / Columns * ItemHeight - VerticalOffset,
                    cellWidth, Math.Min(ItemHeight, ViewportHeight));
            }
        }
        return Rect.Empty;
    }


    public bool CanHorizontallyScroll { get; set; }
    public bool CanVerticallyScroll { get; set; }
    public double ExtentWidth { get; private set; }
    public double ExtentHeight { get; private set; }
    public double ViewportWidth { get; private set; }
    public double ViewportHeight { get; private set; }
    public double HorizontalOffset => 0;
    public double VerticalOffset { get; private set; }
    public ScrollViewer? ScrollOwner { get; set; }
    public void SetHorizontalOffset(double offset) { }
    public void SetVerticalOffset(double offset)
    {
        if (double.IsNaN(offset)) {
            return;
        }
        var next = Math.Clamp(offset, 0, Math.Max(0, ExtentHeight - ViewportHeight));
        if (next == VerticalOffset) {
            return;
        }
        VerticalOffset = next;
        ScrollOwner?.InvalidateScrollInfo();
        InvalidateMeasure();
    }
    public void LineUp() => SetVerticalOffset(VerticalOffset - ItemHeight);
    public void LineDown() => SetVerticalOffset(VerticalOffset + ItemHeight);
    public void PageUp() => SetVerticalOffset(VerticalOffset - ViewportHeight);
    public void PageDown() => SetVerticalOffset(VerticalOffset + ViewportHeight);
    public void MouseWheelUp() => SetVerticalOffset(VerticalOffset - WheelDistance);
    public void MouseWheelDown() => SetVerticalOffset(VerticalOffset + WheelDistance);
    public void LineLeft() { }
    public void LineRight() { }
    public void PageLeft() { }
    public void PageRight() { }
    public void MouseWheelLeft() { }
    public void MouseWheelRight() { }

    /// <summary>
    /// ﾎーーー
    /// </summary>
    private double WheelDistance => SystemParameters.WheelScrollLines < 0 ? ViewportHeight : SystemParameters.WheelScrollLines * ItemHeight;

    #endregion
}
