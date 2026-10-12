using ImogeIndex;
using ImogeIndex.Common;
using ImogeIndex.Mur;
using ImooneWPF.Modelle;
using System.IO;
using Tomlet;
using Tomlet.Attributes;

namespace ImooneWPF.Dictators;

/// <summary>
/// ｳｲﾝﾄﾞｳの種類
/// </summary>
internal enum WindowParamsKey
{
    Main
}

/// <summary>
/// ｳｲﾝﾄﾞｳ
/// </summary>
internal class WindowParams
{
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

    public int Width { get; set; } = 800;
    public int Height { get; set; } = 800;
}

internal delegate void WindowParamsDelegaty(WindowParams prarm);

/// <summary>
/// 詳細ﾍﾟｲﾝのﾊﾟﾗﾒーﾀー
/// </summary>
internal class DetailPaneParams
{
    public bool Visible { get; set; } = false;
    public int Width { get; set; } = 128;
    public bool ShowOriginal { get; set; } = false;
}



/// <summary>
/// 自動保存用設定
/// </summary>
internal class SettingsDictator
{
    [TomlNonSerialized]
    private static SettingsDictator instance = new SettingsDictator();

    [TomlNonSerialized]
    public static SettingsDictator Default => instance;

    [TomlNonSerialized]
    private static readonly Dictionary<WindowParamsKey, WindowParams> WindowParamDefaults = new Dictionary<WindowParamsKey, WindowParams>() {
        {WindowParamsKey.Main, new WindowParams(800, 800) }
    };

    /// <summary>
    /// ｳｲﾝﾄﾞｳｻｲｽﾞなど
    /// </summary>
    public Dictionary<WindowParamsKey, WindowParams> WindowParams { get; set; } = new();

    /// <summary>
    /// 詳細ﾍﾟｲﾝ
    /// </summary>
    public DetailPaneParams DetailPane { get; set; } = new();

    /// <summary>
    /// 前回使ったﾌﾟﾛﾌｧｲﾙ
    /// </summary>
    public string LastProfile { get; set; } = string.Empty;

    /// <summary>
    /// ﾘｽﾄ表示のｶﾗﾑ幅
    /// </summary>
    public int[]? ColumnDimensions { get; set; }

    /// <summary>
    /// ｶﾗﾑ表示/非表示
    /// </summary>
    public bool[]? ColumnVisibility { get; set; }

    /// <summary>
    /// ｶﾗﾑの並び順
    /// </summary>
    public int[]? ColumnOrder { get; set; }

    /// <summary>
    /// ｿーﾄ
    /// </summary>
    public int SortColumn { get; set; } = -1;

    /// <summary>
    /// ｿーﾄ
    /// </summary>
    public bool SortDescending { get; set; }

    /// <summary>
    /// ｲﾝｸﾘﾒﾝﾀﾙ検索有効/無効
    /// </summary>
    public bool IncrementalSearch { get; set; } = true;

    /// <summary>
    /// ﾄﾚｲにぶち込むﾓーﾄﾞ
    /// </summary>
    public bool MinimizeToTray { get; set; } = false;

    /// <summary>
    /// 類似検索に使うﾊｯｼｭ
    /// </summary>
    public string[] SimilarHashes { get; set; } = HashMooneToNames(HashAlgorithmDefault);

    /// <summary>
    /// ﾊｯｼｭのﾃﾞﾌｫﾙﾄ値
    /// </summary>
    [TomlNonSerialized]
    const SimilarHashAlgorithm HashAlgorithmDefault = SimilarHashAlgorithm.PHash | SimilarHashAlgorithm.AHash;

    /// <summary>
    /// 類似検索に使うﾊｯｼｭ(壊れた値や0種ならHashAlgorithmDefault)
    /// </summary>
    [TomlNonSerialized]
    public SimilarHashAlgorithm SimilarHashMoone {
        get {
            var hashes = SimilarHashAlgorithm.None;
            foreach (var x in SimilarHashes ?? []) {
                if (Enum.TryParse<SimilarHashAlgorithm>(x, true, out var amg)) {
                    hashes |= amg;
                }
            }
            return hashes == SimilarHashAlgorithm.None ? HashAlgorithmDefault : hashes;
        }
        set => SimilarHashes = HashMooneToNames(value);
    }

    /// <summary>
    /// ﾊｯｼｭをお名前に
    /// </summary>
    /// <param name="hashes"></param>
    /// <returns></returns>
    static string[] HashMooneToNames(SimilarHashAlgorithm hashes)
    {
        SimilarHashAlgorithm[] singles = [SimilarHashAlgorithm.PHash, SimilarHashAlgorithm.AHash, SimilarHashAlgorithm.DHash, SimilarHashAlgorithm.WHash];
        return singles.Where(x => hashes.HasFlag(x)).Select(x => x.ToString()).ToArray();
    }

    /// <summary>
    /// ﾘｽﾄ表示ﾓーﾄﾞ
    /// </summary>
    public MurDisplayMode FileDisplayMode { get; set; }

    /// <summary>
    /// ﾏーﾃー
    /// </summary>
    public string Theme { get; set; } = ImogeIndex.Web3.Dictators.ThemeDictator.NameOfDefaultTheme;

    /// <summary>
    /// UI言語(空ならOS設定に従う、再起動で反映)
    /// </summary>
    public string Language { get; set; } = ImogeIndex.Web3.Dictators.LanguageDictator.NameOfSystemLanguage;

    /// <summary>
    /// ｴﾗー詳細表示
    /// </summary>
    public bool ShowDetailedError { get; set; } = false;

    /// <summary>
    /// ｳｲﾝﾄﾞｳ個別設定取得
    /// </summary>
    /// <param name="key"></param>
    /// <returns></returns>
    public WindowParams GetWindowParams(WindowParamsKey key)
    {
        if (WindowParams.TryGetValue(key, out var w)) {
            return w;
        }
        return WindowParamDefaults[key];
    }

    /// <summary>
    /// ｳｲﾝﾄﾞｳ個別設定
    /// </summary>
    /// <param name="key"></param>
    /// <param name="delegaty"></param>
    public void WindowParamOf(WindowParamsKey key, WindowParamsDelegaty delegaty) => delegaty(GetWindowParams(key));

    /// <summary>
    /// .DS_Store
    /// </summary>
    /// <returns></returns>
    static FileInfo CreateLocalState() {
        return SystemParams.CreateFrontendLocalStateDir(nameof(ImooneWPF)).CombineFile(".DS_Store");
    }

    /// <summary>
    /// 読み込み後処理
    /// </summary>
    void PostLoad()
    {
        foreach (var x in WindowParamDefaults) {
            if (!WindowParams.ContainsKey(x.Key)) {
                WindowParams.Add(x.Key, new(x.Value));
            }
        }
    }

    /// <summary>
    /// ﾃﾞﾌｫﾙﾄ値を保存
    /// </summary>
    void SaveInternal()
    {
        var sm = CreateLocalState();
        var t = TomletMain.TomlStringFrom(this, TomlSerializerOptions.Default);
        sm.OpenAndWriteText(t);
    }

    /// <summary>
    /// 保存
    /// </summary>
    public static void Save()
    {
        Default?.SaveInternal();
    }

    /// <summary>
    /// 読み込み
    /// </summary>
    public static void Load()
    {
        var sm = CreateLocalState();
        if (!sm.Exists) {
            instance = new();
            instance.PostLoad();
            return;
        }
        try {
            var t = TomlParser.ParseFile(sm.FullName);
            instance = TomletMain.To<SettingsDictator>(t);
        }
        catch {
            instance = new();
        }
        instance.PostLoad();
    }
}
