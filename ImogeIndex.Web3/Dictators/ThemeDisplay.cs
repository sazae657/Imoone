namespace ImogeIndex.Web3.Dictators;

public static class ThemeDisplay {

    /// <summary>
    /// ﾃーﾏ種別を表示用文字列に
    /// </summary>
    /// <param name="theme"></param>
    /// <returns></returns>
    public static string ThemeToString(AppTheme theme) => theme switch
    {
        AppTheme.Light => Properties.Resources.APT_Light,
        AppTheme.Dark => Properties.Resources.APT_Dark,
        AppTheme.Clasico => Properties.Resources.APT_Clasico,
        _ => Properties.Resources.APT_System
    };


    /// <summary>
    /// 表示用文字列をﾃーﾏ種別に
    /// </summary>
    /// <param name="name"></param>
    /// <returns></returns>
    public static AppTheme StringToTheme(string? name) {
        if (string.IsNullOrEmpty(name)) {
            return AppTheme.System;
        }
        if (name == Properties.Resources.APT_Light) {
            return AppTheme.Light;
        }
        if (name == Properties.Resources.APT_Dark) {
            return AppTheme.Dark;
        }
        if (name == Properties.Resources.APT_Clasico) {
            return AppTheme.Clasico;
        }
        return name switch
        {
            nameof(AppTheme.Light) => AppTheme.Light,
            nameof(AppTheme.Dark) => AppTheme.Dark,
            nameof(AppTheme.Clasico) => AppTheme.Clasico,
            _ => AppTheme.System
        };
    }

    /// <summary>
    /// お名前全部
    /// </summary>
    /// <returns></returns>
    public static IEnumerable<string> GetThemeNames() {
        return Enum.GetValues<AppTheme>().Select(ThemeToString);
    }
}
