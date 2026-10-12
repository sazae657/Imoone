using ImogeIndex.Web3.Dictators;
using System.Windows;
using System.Windows.Interop;
namespace ImogeIndex.Web3;

/// <summary>
/// ﾇｴﾝﾄﾞｩ
/// </summary>
public class Nuendow : Window
{
    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        ThemeDictator.ApplyWindowTheme(this);
        var helper = new WindowInteropHelper(this);
        var source = HwndSource.FromHwnd(helper.Handle);
        source?.AddHook(WndProc);
    }

    protected override void OnClosed(EventArgs e) {
        base.OnClosed(e);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        const int WM_SETTINGCHANGE = 0x001A;
        if (msg == WM_SETTINGCHANGE) {
            if (ThemeDictator.CurrentTheme == AppTheme.System) {
                ThemeDictator.ApplyTheme(AppTheme.System);
            }
        }
        return IntPtr.Zero;
    }
}

public class BindingProxy : Freezable {
    public object Data { get => GetValue(DataProperty); set => SetValue(DataProperty, value); }
    public static readonly DependencyProperty DataProperty
        = DependencyProperty.Register(nameof(Data), typeof(object), typeof(BindingProxy),
            new PropertyMetadata(null));

    protected override Freezable CreateInstanceCore() => new BindingProxy();
}
