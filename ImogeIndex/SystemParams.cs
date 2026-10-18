using ImogeIndex.Common;

namespace ImogeIndex;

/// <summary>
/// ﾃﾞﾌｫﾙﾄ値などなど
/// </summary>
public class SystemParams
{
    static SystemParams()
    {
        settingPouth = new DirectoryInfo(
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), ProfileRoot));
    }

    public static void Sanitize()
    {
        var imp = Environment.GetEnvironmentVariable("IMOGEINDEX_HOME");
        if (!string.IsNullOrEmpty(imp)) {
            if (!Directory.Exists(imp)) {
                throw new DirectoryNotFoundException(Properties.Resources.GL_E_DirectoryNotFound.Morfat(imp));
            }
            settingPouth = new DirectoryInfo(Path.Combine(imp, ProfileRoot));
        }
    }

    const string ProfileRoot = "ImogeIndex";

    static DirectoryInfo settingPouth;
    public static  DirectoryInfo SettingPouth => settingPouth;

    internal const string IndexDatumName = "Level.db";
    internal const string ThumbnailDatumName = "Thumbnail.db";
    internal const string LabelDatumName = "Label.db";
    internal const string ConfigName = "Config.toml";
    internal const string DefaultConfigName = ConfigName + ".default";

    internal const string ProfilesOf = "Profile.toml";
    internal const string DefaultProfilesOf = ProfilesOf + ".default";

    internal const string UserDefinitionsName = "User.toml";
    internal const string DefaultUserDefinitionsName = UserDefinitionsName + ".default";

    internal const string DefaultProfileLocation = "Profiles";

    internal const string FrontendSettingRoot = ".DS_Store";

    public static readonly string[] ImageExtensions = [
            @"png", @"jpe?g",
            @"jp2", @"pnm", @"pbm", @"ppm",
            @"webp", @"gif", @"heic",
            @"cr2", @"tiff?", @"psd",
            @"bmp", @"tga", @"dds",
            @"jxr", @"jxl",
            @"wbmp", @"xpm", @"xbm",
            @"avifs?"
        ];

    // 動画対応やる気失せたので使わん
    public static readonly string[] MovieExtensions = [
        "mp4",
        "mov",
        "webm",
        "avi",
        "wmv",
    ];

    public static readonly string[] DisallowExtensions = [
        ".exe", ".bat", ".cmd", ".ps1", ".vbs", ".js", ".jse",
        ".wsf", ".wsh", ".msc", ".msi", ".msp", ".scr", ".pif",
        ".lnk", ".hta", ".cpl", ".reg", ".com", ".dll", ".jar"
    ];

    public const int VersionCurrent = 2;

    /// <summary>
    /// ﾒﾀﾃﾞーﾀﾑ検索ｲﾝﾃﾞｯｸｽのﾃﾞﾌｫ
    /// </summary>
    public static readonly MetaAlias[] DefaultMetaAliases = [
        new MetaAlias{ Tag ="Make", Alias ="make" },
        new MetaAlias{ Tag ="Manufacture", Alias ="make" },
        new MetaAlias{ Tag ="Model", Alias ="model" },
        new MetaAlias{ Tag ="Lens Model", Alias ="lens" },
        new MetaAlias{ Tag ="Software", Alias ="software" },
        new MetaAlias{ Tag ="ISO Speed Ratings", Alias ="iso", ValueType = MetadataValueType.Number },
        new MetaAlias{ Tag ="Exposure Time", Alias ="exposure", ValueType = MetadataValueType.Number },
        new MetaAlias{ Tag ="F-Number", Alias ="aperture", ValueType = MetadataValueType.Number },
        new MetaAlias{ Tag ="Focal Length", Alias ="focal", ValueType = MetadataValueType.Number }

    ];

    /// <summary>
    /// name置き場を作る
    /// </summary>
    /// <param name="name"></param>
    /// <returns></returns>
    public static string CreateLocalStateFile(string name)
    {
        var d = SettingPouth;
        if (!d.Exists) {
            d.Create();
        }
        return Path.Combine(d.FullName, name);
    }

    /// <summary>
    /// nameﾌｫﾙﾀﾞーを作る
    /// </summary>
    /// <param name="name"></param>
    /// <returns></returns>
    public static string CreateLocalStateDir(string name)
    {
        var d = SettingPouth;
        if (!d.Exists) {
            d.Create();
        }
        var cs = Path.Combine(d.FullName, name).AsDirectoryInfo();
        if (!cs.Exists) {
            cs.Create();
        }
        return cs.FullName;
    }

    /// <summary>
    /// ﾌﾟﾛﾌｧｲﾙの最上位を作って取得
    /// </summary>
    /// <returns></returns>
    public static DirectoryInfo SettingRoute()
    {
        var d = SettingPouth;
        if (!d.Exists) {
            d.Create();
        }
        return d;
    }

    /// <summary>
    /// ﾌﾛﾝﾄｴﾝﾄﾞ用設定ﾌｫﾙﾀﾞー作成
    /// </summary>
    /// <param name="partition"></param>
    /// <returns></returns>
    public static DirectoryInfo CreateFrontendLocalStateDir(string partition)
    {
        var d = CreateLocalStateDir(FrontendSettingRoot).CombinePath(partition).AsDirectoryInfo();
        if (!d.Exists) {
            d.Create();
        }
        return d;
    }
}
