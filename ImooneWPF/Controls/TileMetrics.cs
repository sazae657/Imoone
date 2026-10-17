using System.Windows;

namespace ImooneWPF.Controls;

/// <summary>
/// ｱｲｺﾝ表示の寸法
/// </summary>
public static class TileMetrics
{
    public static readonly DependencyProperty IconSizeProperty = DependencyProperty.RegisterAttached(
        "IconSize", typeof(double), typeof(TileMetrics), new PropertyMetadata(32.0));
    public static void SetIconSize(DependencyObject target, double value) => target.SetValue(IconSizeProperty, value);
    public static double GetIconSize(DependencyObject target) => (double)target.GetValue(IconSizeProperty);

    public static readonly DependencyProperty ItemWidthProperty = DependencyProperty.RegisterAttached(
        "ItemWidth", typeof(double), typeof(TileMetrics), new PropertyMetadata(128.0));
    public static void SetItemWidth(DependencyObject target, double value) => target.SetValue(ItemWidthProperty, value);
    public static double GetItemWidth(DependencyObject target) => (double)target.GetValue(ItemWidthProperty);

    public static readonly DependencyProperty ItemHeightProperty = DependencyProperty.RegisterAttached(
        "ItemHeight", typeof(double), typeof(TileMetrics), new PropertyMetadata(64.0));
    public static void SetItemHeight(DependencyObject target, double value) => target.SetValue(ItemHeightProperty, value);
    public static double GetItemHeight(DependencyObject target) => (double)target.GetValue(ItemHeightProperty);
}
