using ImogeIndex.Common;
using ImogeIndex.Mur;
using ImogeIndex.Web6.Modelle;
using Tomlet;
using Tomlet.Attributes;
using R = ImogeIndex.Web6.Properties.Resources;
namespace ImogeIndex.Web6.Dictators;

/// <summary>
/// ｳｲﾝﾄﾞｳの種類
/// </summary>
public enum WindowParamsKey
{
    Main, LabelEdit, Niraction
}

/// <summary>
/// ｳｲﾝﾄﾞｳ
/// </summary>
public class WindowParams
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

public delegate void WindowParamsDelegaty(WindowParams prarm);

/// <summary>
/// 詳細ﾍﾟｲﾝのﾊﾟﾗﾒーﾀー
/// </summary>
public class DetailPaneParams
{
    public bool Visible { get; set; } = false;
    public int Width { get; set; } = 128;
    public bool ShowOriginal { get; set; } = false;
}



/// <summary>
/// 自動保存用設定
/// </summary>
public class SettingsDictator
{
    [TomlNonSerialized]
    private static SettingsDictator instance = new SettingsDictator();

    [TomlNonSerialized]
    public static SettingsDictator Default => instance;

    /// <summary>
    /// 保存先の区画
    /// </summary>
    [TomlNonSerialized]
    private static string? partition;

    [TomlNonSerialized]
    private static readonly Dictionary<WindowParamsKey, WindowParams> WindowParamDefaults = new Dictionary<WindowParamsKey, WindowParams>() {
        {WindowParamsKey.Main, new WindowParams(800, 800) },
        {WindowParamsKey.Niraction, new WindowParams(800, 460) },
        {WindowParamsKey.LabelEdit, new WindowParams(450, 450) },
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
    public MurDisplayMode FileListLayout { get; set; }

    /// <summary>
    /// ｱｲｺﾝ表示の寸法
    /// </summary>
    public int FileIconSize { get; set; } = 32;

    /// <summary>
    /// ﾏーﾃー
    /// </summary>
    public string Theme { get; set; } = NameOfDefaultTheme;

    /// <summary>
    /// ﾃﾞﾌｫﾙﾄﾃーﾏ
    /// </summary>
    [TomlNonSerialized]
    public const string NameOfDefaultTheme = "System";

    /// <summary>
    /// UI言語
    /// </summary>
    public string Language { get; set; } = string.Empty;

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
        if (partition == null) {
            throw new InvalidOperationException(R.DG_E_SaveBeforeLoad.Morfat(nameof(Load)));
        }
        return SystemParams.CreateFrontendLocalStateDir(partition).CombineFile(".DS_Store");
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
    /// <param name="frontend">保存先の区画(ﾌﾛﾝﾄｴﾝﾄﾞ名)</param>
    public static void Load(string frontend)
    {
        partition = frontend;
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
