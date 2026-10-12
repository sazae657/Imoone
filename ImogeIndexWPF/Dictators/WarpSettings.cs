using ImogeIndex;
using ImogeIndex.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows;
using Tomlet;
using Tomlet.Attributes;

namespace ImogeIndexWPF.Dictators;

/// <summary>
/// ｳｲﾝﾄﾞｳｻｲｽ系のﾃﾞﾌｫﾙﾄ
/// </summary>
internal static class WarpDefaults {
    public const int MainWindowWidth  = 548;
    public const int MainWindowHeight = 424;

    public const int ToolWindowWidth = 800;
    public const int ToolWindowHeight = 450;
}

/// <summary>
/// ｳｲﾝﾄﾞｳ
/// </summary>
internal enum WindowParamsKey
{
    Main, Settingure, RegexSpielplatz
}

/// <summary>
/// ｳｲﾝﾄﾞｳ
/// </summary>
internal class WindowParams {
    public WindowParams()
    {
    }

    public WindowParams(WindowParams other)
    {
        Width = other.Width;
        Height = other.Height;
    }

    public WindowParams(int width, int height)
    {
        Width = width;
        Height = height;
    }

    public int Width { get; set; } = WarpDefaults.MainWindowWidth;
    public int Height { get; set; } = WarpDefaults.MainWindowHeight; // 原状高さは使わない
}

internal delegate void WindowParamsDelegaty(WindowParams prarm);

/// <summary>
/// これの設定(自動保存)
/// </summary>
internal class WarpSettings {
    [TomlNonSerialized]
    static WarpSettings instance = new WarpSettings();

    [TomlNonSerialized]
    public static WarpSettings Default => instance;

    [TomlNonSerialized]
    static readonly Dictionary<WindowParamsKey, WindowParams> WindowParamDefaults = new Dictionary<WindowParamsKey, WindowParams>() {
        {WindowParamsKey.Main, new WindowParams(WarpDefaults.MainWindowWidth, WarpDefaults.MainWindowHeight) },
        {WindowParamsKey.Settingure, new WindowParams(WarpDefaults.ToolWindowWidth, WarpDefaults.ToolWindowHeight) },
        {WindowParamsKey.RegexSpielplatz, new WindowParams(WarpDefaults.ToolWindowWidth, WarpDefaults.ToolWindowHeight) },
    };

    /// <summary>
    /// ｳｲﾝﾄﾞｳｻｲｽﾞなど
    /// </summary>
    public Dictionary<WindowParamsKey, WindowParams> WindowParams { get; set; } = new();

    public WindowParams GetWindowParams(WindowParamsKey key)
    {
        if (WindowParams.TryGetValue(key, out var w)) {
            return w;
        }
        return WindowParamDefaults[key];
    }

    public void WindowParamOf(WindowParamsKey key, WindowParamsDelegaty delegaty) => delegaty(GetWindowParams(key));

    /// <summary>
    /// ﾏーﾃー
    /// </summary>
    public string Theme { get; set; } = ImogeIndex.Web3.Dictators.ThemeDictator.NameOfDefaultTheme;

    /// <summary>
    /// UI言語(空ならOS設定に従う、再起動で反映)
    /// </summary>
    public string Language { get; set; } = ImogeIndex.Web3.Dictators.LanguageDictator.NameOfSystemLanguage;

    /// <summary>
    /// ﾀｽｸﾄﾚｲにぶち込むﾓーﾄﾞ
    /// </summary>
    public bool MinimizeToTray { get; set; } = false;

    /// <summary>
    /// ｴﾗー詳細表示
    /// </summary>
    public bool ShowDetailedError { get; set; } = false;

    static FileInfo CreateLocalState() {
        return SystemParams.CreateFrontendLocalStateDir(nameof(ImogeIndexWPF)).CombineFile(".DS_Store");
    }

    void SaveInternal() {
        var sm = CreateLocalState();
        var t = TomletMain.TomlStringFrom(this, TomlSerializerOptions.Default);
        sm.OpenAndWriteText(t);
    }

    void PostLoad()
    {
        foreach (var x in WindowParamDefaults) {
            if (!WindowParams.ContainsKey(x.Key)) {
                WindowParams.Add(x.Key, new WindowParams(x.Value));
            }
        }
    }

    public static void Save() {
        Default?.SaveInternal();
    }

    public static void Load() {
        var sm = CreateLocalState();
        if (!sm.Exists) {
            instance = new();
            instance.PostLoad();
            return;
        }
        try {
            var t = TomlParser.ParseFile(sm.FullName);
            instance = TomletMain.To<WarpSettings>(t);
        }
        catch {
            instance = new();
        }
        instance.PostLoad();
    }
}
