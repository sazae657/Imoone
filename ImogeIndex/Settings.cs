using ImogeIndex.Captionizers;
using ImogeIndex.Common;
using ImogeIndex.Niraction;
using Microsoft.ML.OnnxRuntime;
using System.Collections;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Tomlet;
using Tomlet.Attributes;

namespace ImogeIndex;

public enum CaptionizerType {
    Ollama, LLamaCpp, OpenAICompat
}

public class CaptionizeSetting {
    [TomlPrecedingComment("キャプショニングに使うLLM")]
    public CaptionizerType Type { get; set; } = CaptionizerType.Ollama;

    [TomlPrecedingComment("有効/無効")]
    public bool Enable { get; set; }

    [TomlPrecedingComment("HTTPタイムアウト(秒)")]
    public int HttpTimeout { get; set; } = 180;

    [TomlPrecedingComment("連続でリクエストが失敗したらキャプショニングを打ち切る回数(0で無効)")]
    public int AbortThreshold { get; set; } = 5;

    [TomlPrecedingComment("そこもっと")]
    public List<string> MoreHere { get; set; } = new();

    [TomlPrecedingComment("そこ見ない")]
    public List<string> NoMoreHere { get; set; } = new();

    [TomlPrecedingComment("Ollama設定")]
    public OllamaSetting Ollama { get; set; } = new();

    [TomlPrecedingComment("llama.cpp設定")]
    public LLamaCppSetting LLamaCpp { get; set; } = new();

    [TomlPrecedingComment("OpenAI互換の設定")]
    public OpenAICompatSetting OpenAICompat { get; set; } = new();
}

public enum TaggerType {
    WD, Camie
}

public class TaggerSetting {
    [TomlPrecedingComment("有効/無効")]
    public bool Enable { get; set; } = true;

    [TomlPrecedingComment("Taggerの種類")]
    public TaggerType Type { get; set; } = TaggerType.WD;

    [TomlPrecedingComment("model.onnxへのパス")]
    public string Model { get; set; } = "";

    [TomlPrecedingComment("タグもしくはメタデータのファイル名")]
    public string Tags { get; set; } = "selected_tags.csv";

    [TomlPrecedingComment("モデルのコンパイル")]
    public bool Modepile { get; set; } = true;

    [TomlPrecedingComment("タグの閾値")]
    public float Threshold { get; set; } = 0.35f;
    [TomlPrecedingComment("キャラクターの閾値")]
    public float CharacterThreshold { get; set; } = 0.1f;

    [TomlPrecedingComment("計算に使うデバイス")]
    public ExecutionProviderDevicePolicy DevicePolicy { get; set; } = ExecutionProviderDevicePolicy.MAX_PERFORMANCE;

    [TomlPrecedingComment("スレッド数")]
    public int Threads { get; set; } = 1;
}

public class ThumbnailSetting {
    [TomlPrecedingComment("有効/無効")]
    public bool Enable { get; set; } = true;

    [TomlPrecedingComment("サムネイルの長辺")]
    public int Size { get; set; } = 256;
}

/// <summary>
/// ﾒﾀﾃﾞーﾀﾑの検索型
/// </summary>
public enum MetadataValueType { Text, Number }

/// <summary>
/// ﾒﾀﾃﾞーﾀﾑのｴｲﾘｱｽと検索型
/// </summary>
public class MetaAlias : IEquatable<MetaAlias> {
    public string Tag { get; set; } = string.Empty;
    public string Alias { get; set; } = string.Empty;
    public MetadataValueType ValueType { get; set; }

    /// <summary>
    /// 設定画面用に
    /// </summary>
    /// <returns></returns>
    public string ToSettingure() { return $"{Alias}:{ValueType.ToString().ToLowerInvariant()}={Tag}"; }

    /// <summary>
    /// 設定画面から復元
    /// </summary>
    /// <param name="src"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    public static MetaAlias FromSettingure(string src) {
        var ms = src.IndexOf('=');
        if (ms < 0) {
            throw new ArgumentException(Properties.Resources.GL_E_MP_Assign.Morfat(src));
        }
        var key = src.Substring(0, ms).Trim();
        var separator = key.IndexOf(':');
        if (separator <= 0) {
            throw new ArgumentException(Properties.Resources.GL_E_MP_TypeSyntax.Morfat(src));
        }
        var alias = key[..separator].Trim();
        var typer = key[(separator + 1)..].Trim().ToLowerInvariant() switch {
            "text" => MetadataValueType.Text,
            "number" => MetadataValueType.Number,
            _ => throw new ArgumentException(Properties.Resources.GL_E_MP_InvalidType.Morfat(src)),
        };
        var tag = src.Substring(ms + 1).Trim();
        if (string.IsNullOrEmpty(alias) || string.IsNullOrEmpty(tag)) {
            throw new ArgumentException(Properties.Resources.GL_E_MP_InvalidGrade.Morfat(src));
        }
        return new MetaAlias {
            Alias = alias,
            Tag = tag,
            ValueType = typer,
        };
    }

    public static void ValidateType(IEnumerable<MetaAlias> aliases) {
        foreach (var group in aliases.GroupBy(x => x.Alias.Trim(), StringComparer.OrdinalIgnoreCase)) {
            if (group.Any(x => !Enum.IsDefined(x.ValueType)) || group.Select(x => x.ValueType).Distinct().Count() != 1) {
                throw new ArgumentException(Properties.Resources.GL_E_MP_BothType.Morfat(group.Key));
            }
        }
    }

    #region IEquatable

    public bool Equals(MetaAlias? other) {
        if (ReferenceEquals(this, other)) {
            return true;
        }
        if (other is null) {
            return false;
        }
        return ValueType == other.ValueType && string.Equals(Tag, other.Tag, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(Alias, other.Alias, StringComparison.OrdinalIgnoreCase);
    }

    public override bool Equals(object? obj) => obj is MetaAlias other && Equals(other);

    public override int GetHashCode() =>
        HashCode.Combine(StringComparer.OrdinalIgnoreCase.GetHashCode(Tag), StringComparer.OrdinalIgnoreCase.GetHashCode(Alias), ValueType);
    #endregion
}

public class Settings : ICustomVariableJail {
    static Settings() {
    }

    private static readonly MethodInfo MemberwiseCloneMethod = typeof(object).GetMethod(
        "MemberwiseClone",
        BindingFlags.Instance | BindingFlags.NonPublic)!;
    private static IEnumerable<string> GetDefaultExtension() => SystemParams.ImageExtensions;


    /// <summary>
    /// TOMLから込み込み
    /// </summary>
    /// <param name="key"></param>
    /// <param name="path"></param>
    /// <returns></returns>
    public static PreparedSettings Load(string key, DirectoryInfo path) {
        StringExtensions.Setup();
        var tmp = new Settings();
        var sm = path.CombineFile(SystemParams.ConfigName);
        if (!sm.Directory!.Exists) {
            sm.Directory.Create();
        }
        if (!sm.Exists) {
            tmp.SaveInternal(path);
        }
        tmp.SaveDefault(path);

        var t = TomlParser.ParseFile(sm.FullName);
        var rc = TomletMain.To<Settings>(t);
        rc.SettingFilePath = sm;
        rc.IPv7 = key;
        rc.UserDefinitions = UserDefinitions.Load(key , path);
        return rc.Prepare(path);
    }

    public Settings() { }

    [TomlNonSerialized]
    public DirectoryInfo? Root { get; set; }

    [TomlNonSerialized]
    public FileInfo? SettingFilePath { get; private set; }

    [TomlNonSerialized]
    public string? IPv7 { get; set; }

    [TomlNonSerialized]
    public UserDefinitions? UserDefinitions { get; set; }


    [TomlPrecedingComment("検索するパス")]
    public List<string> SearchPaths { get; set; } = [@"${Env:USERPROFILE}\Pictures"];

    [TomlPrecedingComment("除外するパス")]
    public List<string> ExcludePaths { get; set; } = [];

    [TomlPrecedingComment("除外パターン(正規表現)")]
    public List<string> ExcludePatterns { get; set; } = [];

    [TomlPrecedingComment("画像の拡張子(正規表現)")]
    public List<string> ImageExtensions { get; set; } = [];

    [TomlPrecedingComment("検索インデックスを作成するメタデータムタグ")]
    public List<MetaAlias> MetadataSearchTags { get; set; } = [];

    //[TomlNonSerialized]
    //public List<Regex> MovieExtsnsions { get; set; } = [];

    [TomlPrecedingComment("ログレベル")]
    public int LogLevel { get; set; } = 4;
    [TomlPrecedingComment("画像処理のスレッド数")]
    public int Threads { get; set; } = 4;

    [TomlPrecedingComment("画像処理でメモリに溜め込む枚数/スレッド")]
    public int BufferCount { get; set; } = 25;

    [TomlPrecedingComment("キャッシュフォルダー")]
    public string CacheDir { get; set; } = @"${IG:CONFIG_ROOT}\Cache";

    [TomlPrecedingComment("ﾆﾗｸｼｮﾝ設定")]
    public NiractionSetting Niraction { get; set; } = new();

    [TomlPrecedingComment("サムネイルの設定")]
    public ThumbnailSetting Thumbnail { get; set; } = new();

    [TomlPrecedingComment("Taggerの設定")]
    public TaggerSetting Tagger { get; set; } = new();

    [TomlPrecedingComment("キャプショニング設定")]
    public CaptionizeSetting Captioning { get; set; } = new();

    /// <summary>
    /// 保存
    /// </summary>
    /// <param name="root"></param>
    void SaveInternal(DirectoryInfo root) {
        ImageExtensions = [.. GetDefaultExtension()];
        var sm = root.CombineFile(SystemParams.ConfigName);
        var t = TomletMain.TomlStringFrom(this, TomlSerializerOptions.Default);
        sm.OpenAndWriteText(t);
    }

    /// <summary>
    /// ﾃﾞﾌｫﾙﾄﾃﾝﾌﾟﾚの保存
    /// </summary>
    /// <param name="root"></param>
    void SaveDefault(DirectoryInfo root) {
        ImageExtensions = [.. GetDefaultExtension()];
        var sm = root.CombineFile(SystemParams.DefaultConfigName);
        var t = TomletMain.TomlStringFrom(this, TomlSerializerOptions.Default);
        sm.OpenAndWriteText(t);
    }

    /// <summary>
    /// 上書き保存
    /// </summary>
    /// <returns></returns>
    public async Task Jail() {
        if (SettingFilePath == null) {
            return;
        }
        var t = TomletMain.TomlStringFrom(this, TomlSerializerOptions.Default);
        await SettingFilePath.WriteAndStoreAsync(fs => fs.WriteAsync(Encoding.UTF8.GetBytes(t)).AsTask());
    }

    const string EnvGroup = "Env";
    const string ImogeGroup = "IG";

    /// <summary>
    /// 変数展開
    /// </summary>
    /// <param name="group"></param>
    /// <param name="name"></param>
    /// <param name="val"></param>
    /// <returns></returns>
    bool ICustomVariableJail.Get(string group, string name, out string? val) {
        if (group.Equals(EnvGroup, StringComparison.OrdinalIgnoreCase)) {
            // 環境変数の値を取得
            var envValue = Environment.GetEnvironmentVariable(name);
            if (envValue != null) {
                val = envValue;
                return true;
            }
            val = null;
            return false;
        }
        if (envVars.TryGetValue(group, out var dm) &&
            dm.TryGetValue(name, out val)) {
            return true;
        }
        val = null;
        return false;
    }

    /// <summary>
    /// ｴｷﾊﾟﾝする
    /// </summary>
    /// <param name="str"></param>
    /// <returns></returns>
    public string ExpandingClass(string str) {
        return str.ExpandCustomVariables(this);
    }

    Dictionary<string, Dictionary<string, string>> envVars = new();

    /// <summary>
    /// PreparedSettingsの組み立て
    /// </summary>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    public PreparedSettings Prepare() {
        if (Root == null) {
            throw new InvalidOperationException(Properties.Resources.GL_E_RootIsNull);
        }
        return Prepare(Root);
    }

    /// <summary>
    /// 禁止拡張子にﾏｯﾁするものを除外してｺﾝﾊﾟｲﾙ
    /// </summary>
    /// <param name="extensions"></param>
    /// <returns></returns>
    internal static Regex[] CompileImageExtensions(IEnumerable<string> extensions) {
        return extensions
            .Select(w => $"\\.{w}\\Z".AsRegexIgnoreCase())
            .Where(r => !SystemParams.DisallowExtensions.Any(r.IsMatch))
            .ToArray();
    }

    /// <summary>
    /// ｺﾞﾆｮｺﾞﾆｮやってPreparedSettings化
    /// </summary>
    /// <param name="root"></param>
    /// <returns></returns>
    PreparedSettings Prepare(DirectoryInfo root) {
        envVars.Clear();
        envVars.Add(EnvGroup, new());
        var ig = new Dictionary<string, string>{
                    { "CONFIG_ROOT", SystemParams.SettingRoute().FullName },
                    { "PROFILE_ROOT", root.FullName }
                };
        envVars.Add(ImogeGroup, ig);

        Root = root;
        var searchPaths = SearchPaths
            .Where(w => !string.IsNullOrWhiteSpace(w))
            .Select(w => w.ExpandCustomVariables(this))
            .ToArray();
        var excludePaths = ExcludePaths.Select(w => w.ExpandCustomVariables(this)).ToArray();
        var excludePatterns = ExcludePatterns.Select(w => w.AsRegexIgnoreCase()).ToArray();

        if (ImageExtensions.Count == 0) {
            ImageExtensions.AddRange(SystemParams.ImageExtensions);
        }
        var imageExtsnsions = CompileImageExtensions(ImageExtensions);
        var cacheDirectory = CacheDir.ExpandCustomVariables(this).AsDirectoryInfo();
        var moreHere = Captioning.MoreHere.Select(w => w.AsRegexIgnoreCase()).ToArray();
        var noMoreHere = Captioning.NoMoreHere.Select(w => w.AsRegexIgnoreCase()).ToArray(); 
        return new PreparedSettings(this, searchPaths, excludePaths, excludePatterns, imageExtsnsions, moreHere, noMoreHere, cacheDirectory);
    }

    /// <summary>
    /// ｺﾋﾟｨ
    /// </summary>
    /// <returns></returns>
    public Settings DeepCopy() {
        return (Settings)DeepCopyValue(this, new Dictionary<object, object>(ReferenceEqualityComparer.Instance))!;
    }

    #region 超絶コピィ
    private static object? DeepCopyValue(object? source, IDictionary<object, object> copiedObjects) {
        if (source is null) {
            return null;
        }

        var type = source.GetType();
        if (type.IsValueType || source is string || source is Regex || source is Type || source is Delegate) {
            return source;
        }
        if (copiedObjects.TryGetValue(source, out var copiedObject)) {
            return copiedObject;
        }
        if (source is DirectoryInfo directoryInfo) {
            return new DirectoryInfo(directoryInfo.FullName);
        }
        if (source is FileInfo fileInfo) {
            return new FileInfo(fileInfo.FullName);
        }
        if (source is Array array) {
            var copiedArray = (Array)array.Clone();
            copiedObjects[source] = copiedArray;
            for (var i = 0; i < array.Length; i++) {
                copiedArray.SetValue(DeepCopyValue(array.GetValue(i), copiedObjects), i);
            }
            return copiedArray;
        }
        if (source is IDictionary dictionary) {
            var copiedDictionary = (IDictionary)Activator.CreateInstance(type)!;
            copiedObjects[source] = copiedDictionary;
            foreach (DictionaryEntry item in dictionary) {
                copiedDictionary.Add(
                    DeepCopyValue(item.Key, copiedObjects)!,
                    DeepCopyValue(item.Value, copiedObjects));
            }
            return copiedDictionary;
        }
        if (source is IList list) {
            var copiedList = (IList)Activator.CreateInstance(type)!;
            copiedObjects[source] = copiedList;
            foreach (var item in list) {
                copiedList.Add(DeepCopyValue(item, copiedObjects));
            }
            return copiedList;
        }

        var copy = MemberwiseCloneMethod.Invoke(source, null)!;
        copiedObjects[source] = copy;
        for (var currentType = type; currentType is not null; currentType = currentType.BaseType) {
            foreach (var field in currentType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)) {
                field.SetValue(copy, DeepCopyValue(field.GetValue(source), copiedObjects));
            }
        }
        return copy;
    }
    #endregion

}
