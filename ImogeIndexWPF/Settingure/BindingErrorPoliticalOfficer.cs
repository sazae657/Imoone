using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace ImogeIndexWPF.Settingure;

/// <summary>
/// バインディングのエラー状態が変わったことを通知するルーティングイベントの引数
/// </summary>
public static class BindingErrorPoliticalOfficer
{
    /// <summary>
    /// TextBoxのValidation.HasErrorが変化したときに親へ伝播させる
    /// </summary>
    public static readonly RoutedEvent ErrorChangedEvent = EventManager.RegisterRoutedEvent(
        "ErrorChanged", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(BindingErrorPoliticalOfficer));

    public static void AddErrorChangedHandler(DependencyObject d, RoutedEventHandler handler)
    {
        (d as UIElement)?.AddHandler(ErrorChangedEvent, handler);
    }

    public static void RemoveErrorChangedHandler(DependencyObject d, RoutedEventHandler handler)
    {
        (d as UIElement)?.RemoveHandler(ErrorChangedEvent, handler);
    }

    public static readonly DependencyProperty EnabledProperty = DependencyProperty.RegisterAttached(
        "Enabled", typeof(bool), typeof(BindingErrorPoliticalOfficer), new PropertyMetadata(false, OnEnabledChanged));

    public static bool GetEnabled(DependencyObject d) => (bool)d.GetValue(EnabledProperty);

    public static void SetEnabled(DependencyObject d, bool value) => d.SetValue(EnabledProperty, value);

    /// <summary>
    /// Validation.HasErrorを自分自身からバインドするやつ(AddValueChangedの強参照掴みっぱ対策)
    /// </summary>
    private static readonly DependencyProperty HasErrorMirrorProperty = DependencyProperty.RegisterAttached(
        "HasErrorMirror", typeof(bool), typeof(BindingErrorPoliticalOfficer), new PropertyMetadata(false, OnHasErrorMirrorChanged));

    private static void OnEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not FrameworkElement element) {
            return;
        }
        if ((bool)e.NewValue) {
            element.SetBinding(HasErrorMirrorProperty, new Binding {
                Path = new PropertyPath(Validation.HasErrorProperty),
                RelativeSource = RelativeSource.Self,
                Mode = BindingMode.OneWay,
            });
        }
        else {
            BindingOperations.ClearBinding(element, HasErrorMirrorProperty);
        }
    }

    private static void OnHasErrorMirrorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is FrameworkElement element) {
            element.RaiseEvent(new RoutedEventArgs(ErrorChangedEvent, element));
        }
    }

    /// <summary>
    /// 配下にバインディングエラーを持つ要素があるか
    /// 値の型変換に失敗した欄はモデレへ届かないので画面側で調べる
    /// </summary>
    /// <param name="root"></param>
    /// <returns></returns>
    public static bool HasError(DependencyObject root)
    {
        if (Validation.GetHasError(root)) {
            return true;
        }
        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++) {
            if (HasError(VisualTreeHelper.GetChild(root, i))) {
                return true;
            }
        }
        return false;
    }
}
