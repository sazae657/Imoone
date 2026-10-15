using Microsoft.Win32;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace ImogeIndex.Web3.Dictators;

/// <summary>
/// ﾃーﾏの適用と切り替えを独裁
/// </summary>
public static class ThemeDictator
{
    private static readonly Uri LightThemeUri = new Uri("/ImogeIndex.Web3;component/Themes/Light.xaml", UriKind.RelativeOrAbsolute);
    private static readonly Uri ClasicoThemeUri = new Uri("/ImogeIndex.Web3;component/Themes/Clasico.xaml", UriKind.RelativeOrAbsolute);
    private static readonly Uri DarkThemeUri = new Uri("/ImogeIndex.Web3;component/Themes/Dark.xaml", UriKind.RelativeOrAbsolute);

    /// <summary>
    /// ﾃﾞﾌｫﾙﾄﾃーﾏ
    /// </summary>
    public static string NameOfDefaultTheme => nameof(AppTheme.System);

    /// <summary>
    /// 現在設定されているﾃーﾏ
    /// </summary>
    public static AppTheme CurrentTheme { get; private set; } = AppTheme.System;

    /// <summary>
    /// 現在実際に適用されているのが黒ﾃーﾏかどうか
    /// </summary>
    public static bool IsDarkTheme { get; private set; }

    /// <summary>
    /// ﾃーﾏが変更された
    /// </summary>
    public static event EventHandler? ThemeChanged;

    static ThemeDictator()
    {
        try
        {
            // OSのﾃーﾏ設定変更監視
            SystemEvents.UserPreferenceChanged += (s, e) =>
            {
                if (e.Category == UserPreferenceCategory.General && CurrentTheme == AppTheme.System)
                {
                    Application.Current?.Dispatcher.BeginInvoke(() =>
                    {
                        ApplyTheme(AppTheme.System);
                    });
                }
            };
        }
        catch {
            // 無視
        }
    }

    /// <summary>
    /// OSがﾀﾞーｸﾓーﾄﾞかどうかを取得
    /// </summary>
    public static bool IsSystemInDarkMode()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            var val = key?.GetValue("AppsUseLightTheme");
            if (val is int intVal)
            {
                return intVal == 0;
            }
        }
        catch {
        }
        return false;
    }


    /// <summary>
    /// 指定したﾃーﾏを適用
    /// </summary>
    /// <param name="theme"></param>
    public static void ApplyTheme(AppTheme theme)
    {
        CurrentTheme = theme;
        bool isDark = theme switch
        {
            AppTheme.Dark => true,
            AppTheme.Light or AppTheme.Clasico => false,
            _ => IsSystemInDarkMode()
        };
        IsDarkTheme = isDark;

        var targetUri = theme == AppTheme.Clasico ? ClasicoThemeUri : isDark ? DarkThemeUri : LightThemeUri;
        var appResources = Application.Current?.Resources;
        if (appResources != null)
        {
            var existingThemeDict = appResources.MergedDictionaries.FirstOrDefault(d =>
                d.Source == LightThemeUri || d.Source == DarkThemeUri || d.Source == ClasicoThemeUri);

            var newThemeDict = new ResourceDictionary { Source = targetUri };
            if (existingThemeDict != null)
            {
                int index = appResources.MergedDictionaries.IndexOf(existingThemeDict);
                appResources.MergedDictionaries.RemoveAt(index);
                appResources.MergedDictionaries.Insert(index, newThemeDict);
            }
            else
            {
                appResources.MergedDictionaries.Insert(0, newThemeDict);
            }
        }

        // 開いている全ｳｨﾝﾄﾞｳのﾀｲﾄﾙﾊﾞーに黒ﾓーﾄﾞを適用
        if (Application.Current != null)
        {
            foreach (Window window in Application.Current.Windows)
            {
                ApplyWindowTheme(window);
            }
        }

        ThemeChanged?.Invoke(null, EventArgs.Empty);
    }


    /// <summary>
    /// 対象ｳｨﾝﾄﾞｳのﾀｲﾄﾙﾊﾞーの黒ﾓーﾄﾞ属性を更新
    /// </summary>
    /// <param name="window"></param>
    public static void ApplyWindowTheme(Window window)
    {
        if (window == null) return;
        var helper = new WindowInteropHelper(window);
        IntPtr hwnd = helper.Handle;
        if (hwnd == IntPtr.Zero)
        {
            window.SourceInitialized -= Window_SourceInitialized;
            window.SourceInitialized += Window_SourceInitialized;
            return;
        }
        SetWindowDarkModeInternal(hwnd, IsDarkTheme);
    }

    private static void Window_SourceInitialized(object? sender, EventArgs e)
    {
        if (sender is Window window)
        {
            window.SourceInitialized -= Window_SourceInitialized;
            var helper = new WindowInteropHelper(window);
            if (helper.Handle != IntPtr.Zero)
            {
                SetWindowDarkModeInternal(helper.Handle, IsDarkTheme);
            }
        }
    }

    private static void SetWindowDarkModeInternal(IntPtr hwnd, bool isDark)
    {
        int useDarkMode = isDark ? 1 : 0;
        const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
        const int DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1 = 19;

        if (NativeMethods.DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref useDarkMode, sizeof(int)) != 0)
        {
            NativeMethods.DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1, ref useDarkMode, sizeof(int));
        }
    }

}

internal static class NativeMethods
{
    [DllImport("dwmapi.dll", PreserveSig = true)]
    public static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);
}
