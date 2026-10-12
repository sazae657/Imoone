using ImogeIndex;
using ImogeIndex.Common;
using System.Collections;
using System.Reflection;
using Tomlet;
using Tomlet.Attributes;
using R = ImogeMerge.Properties.Resources;

namespace ImogeMerge;

/// <summary>
/// Config.tomlのﾏーｼﾞ
/// ﾘｽﾄは和集合、それ以外はC優先で食い違いは警告
/// </summary>
public static class SettingsMerger {
    // 和集合を取るので差分ﾁｪｯｸしない項目
    static readonly HashSet<string> UnionProperties = [
        nameof(Settings.SearchPaths), nameof(Settings.ExcludePaths), nameof(Settings.ExcludePatterns),
        nameof(Settings.ImageExtensions), nameof(Settings.MetadataSearchTags),
    ];

    // 警告に値を出さない項目
    static readonly HashSet<string> SecretProperties = ["ApiKey"];

    /// <summary>
    /// 変数展開(ﾊﾟｽの比較用に環境変数だけ)
    /// </summary>
    sealed class EnvVariables : ICustomVariableJail {
        public bool Get(string group, string name, out string? val) {
            if (group.Equals("Env", StringComparison.OrdinalIgnoreCase)) {
                val = Environment.GetEnvironmentVariable(name);
                return val != null;
            }
            val = null;
            return false;
        }
    }

    /// <summary>
    /// 設定ﾌｧｲﾙ読み込み(変数展開しない素の状態)
    /// </summary>
    /// <param name="file"></param>
    /// <returns></returns>
    public static Settings Read(FileInfo file) {
        return TomletMain.To<Settings>(TomlParser.ParseFile(file.FullName));
    }

    /// <summary>
    /// 書き出し
    /// </summary>
    /// <param name="settings"></param>
    /// <param name="file"></param>
    public static void Write(Settings settings, FileInfo file) {
        var t = TomletMain.TomlStringFrom(settings, TomlSerializerOptions.Default);
        file.OpenAndWriteText(t);
    }

    /// <summary>
    /// ﾏーｼﾞ
    /// </summary>
    /// <param name="century"></param>
    /// <param name="bongo"></param>
    /// <param name="warnings"></param>
    /// <returns></returns>
    public static Settings Merge(Settings century, Settings bongo, List<string> warnings) {
        var rc = century.DeepCopy();
        // ﾊﾟｽは大文字小文字を区別しない
        rc.SearchPaths = Union(century.SearchPaths, bongo.SearchPaths, StringComparer.OrdinalIgnoreCase);
        rc.ExcludePaths = Union(century.ExcludePaths, bongo.ExcludePaths, StringComparer.OrdinalIgnoreCase);
        rc.ExcludePatterns = Union(century.ExcludePatterns, bongo.ExcludePatterns, StringComparer.Ordinal);
        rc.ImageExtensions = Union(century.ImageExtensions, bongo.ImageExtensions, StringComparer.OrdinalIgnoreCase);
        rc.MetadataSearchTags = MergeMetaAliases(century.MetadataSearchTags, bongo.MetadataSearchTags, warnings);

        CheckExcludeOverlap(bongo.ExcludePaths, century, warnings);
        CheckExcludeOverlap(century.ExcludePaths, bongo, warnings);
        foreach (var p in bongo.ExcludePatterns.Where(w => !century.ExcludePatterns.Contains(w, StringComparer.Ordinal))) {
            warnings.Add(R.PMG_W_ExcludePatternAdded.Morfat(p));
        }
        foreach (var p in century.ExcludePatterns.Where(w => !bongo.ExcludePatterns.Contains(w, StringComparer.Ordinal))) {
            warnings.Add(R.PMG_W_ExcludePatternAdded.Morfat(p));
        }

        if (IsModelMixed(century, bongo)) {
            warnings.Add(R.PMG_W_ModelMixed);
        }
        Compare(typeof(Settings), century, bongo, string.Empty, warnings);
        return rc;
    }


    /// <summary>
    /// 順番を保ったまま和集合
    /// </summary>
    /// <param name="century"></param>
    /// <param name="bongo"></param>
    /// <param name="comparer"></param>
    /// <returns></returns>
    static List<string> Union(IEnumerable<string> century, IEnumerable<string> bongo, StringComparer comparer) {
        return century.Concat(bongo)
            .Where(w => !string.IsNullOrWhiteSpace(w))
            .Distinct(comparer)
            .ToList();
    }


    /// <summary>
    /// ﾒﾀﾃﾞーﾀﾑｴｲﾘｱｽのﾏーｼﾞ
    /// 型が食い違うBのｴｲﾘｱｽは捨てる
    /// </summary>
    /// <param name="century"></param>
    /// <param name="bongo"></param>
    /// <param name="warnings"></param>
    /// <returns></returns>
    static List<MetaAlias> MergeMetaAliases(List<MetaAlias> century, List<MetaAlias> bongo, List<string> warnings) {
        var rc = century.Distinct().ToList();
        foreach (var m in bongo) {
            if (rc.Contains(m)) {
                continue;
            }
            var conflict = rc.FirstOrDefault(w =>
                string.Equals(w.Alias.Trim(), m.Alias.Trim(), StringComparison.OrdinalIgnoreCase) && w.ValueType != m.ValueType);
            if (conflict != null) {
                warnings.Add(R.PMG_W_MetaTypeConflict.Morfat(m.ToSettingure(), conflict.ToSettingure()));
                continue;
            }
            rc.Add(m);
        }
        return rc;
    }


    /// <summary>
    /// 片方の除外ﾊﾟｽがもう片方の検索ﾊﾟｽと重なってないか
    /// </summary>
    /// <param name="excludes"></param>
    /// <param name="other"></param>
    /// <param name="warnings"></param>
    static void CheckExcludeOverlap(List<string> excludes, Settings other, List<string> warnings) {
        var env = new EnvVariables();
        foreach (var e in excludes) {
            if (other.ExcludePaths.Contains(e, StringComparer.OrdinalIgnoreCase)) {
                continue;
            }
            var ex = NormalizePath(e.ExpandCustomVariables(env));
            foreach (var s in other.SearchPaths) {
                var sp = NormalizePath(s.ExpandCustomVariables(env));
                if (IsUnder(ex, sp) || IsUnder(sp, ex)) {
                    warnings.Add(R.PMG_W_ExcludeOverlap.Morfat(e, s));
                }
            }
        }
    }

    static string NormalizePath(string path) => path.Replace('/', '\\').TrimEnd('\\');

    static bool IsUnder(string child, string parent) {
        return child.Equals(parent, StringComparison.OrdinalIgnoreCase)
            || child.StartsWith(parent + "\\", StringComparison.OrdinalIgnoreCase);
    }



    /// <summary>
    /// ﾀｸﾞやｷｬﾌﾟｼｮﾝの出処が混ざる？
    /// </summary>
    /// <param name="century"></param>
    /// <param name="bongo"></param>
    /// <returns></returns>
    static bool IsModelMixed(Settings century, Settings bongo) {
        if (century.Tagger.Type != bongo.Tagger.Type ||
            !string.Equals(century.Tagger.Model, bongo.Tagger.Model, StringComparison.OrdinalIgnoreCase)) {
            return true;
        }
        var cc = century.Captioning;
        var cb = bongo.Captioning;
        if (cc.Type != cb.Type) {
            return true;
        }
        return cc.Type switch {
            CaptionizerType.Ollama => cc.Ollama.Model != cb.Ollama.Model,
            CaptionizerType.OpenAICompat => cc.OpenAICompat.Model != cb.OpenAICompat.Model,
            // llama.cppはｻーﾊﾞー側でﾓﾃﾞﾙが決まるので接続先で見る
            CaptionizerType.LLamaCpp => cc.LLamaCpp.Host != cb.LLamaCpp.Host,
            _ => false,
        };
    }


    /// <summary>
    /// 食い違ってる項目を総ﾅﾒ
    /// </summary>
    /// <param name="type"></param>
    /// <param name="century"></param>
    /// <param name="bongo"></param>
    /// <param name="prefix"></param>
    /// <param name="warnings"></param>
    static void Compare(Type type, object century, object bongo, string prefix, List<string> warnings) {
        foreach (var p in type.GetProperties(BindingFlags.Public | BindingFlags.Instance)) {
            if (!p.CanRead || !p.CanWrite || p.GetIndexParameters().Length > 0 ||
                p.GetCustomAttribute<TomlNonSerializedAttribute>() != null) {
                continue;
            }
            if (prefix.Length == 0 && UnionProperties.Contains(p.Name)) {
                continue;
            }
            var name = prefix + p.Name;
            var vc = p.GetValue(century);
            var vb = p.GetValue(bongo);
            var pt = p.PropertyType;
            if (IsScalar(pt) || vc == null || vb == null) {
                if (!Equals(vc, vb)) {
                    warnings.Add(Differs(name, p.Name, vc, vb));
                }
            }
            else if (vc is IEnumerable ec && vb is IEnumerable eb) {
                var sa = Stringify(ec);
                var sb = Stringify(eb);
                if (sa != sb) {
                    warnings.Add(Differs(name, p.Name, sa, sb));
                }
            }
            else {
                Compare(pt, vc, vb, name + ".", warnings);
            }
        }
    }

    static bool IsScalar(Type t) {
        var u = Nullable.GetUnderlyingType(t) ?? t;
        return u.IsPrimitive || u.IsEnum || u == typeof(string) || u == typeof(decimal);
    }

    static string Stringify(IEnumerable e) => "[" + string.Join(", ", e.Cast<object?>().Select(w => w?.ToString())) + "]";

    static string Differs(string name, string leaf, object? vc, object? vb) {
        if (SecretProperties.Contains(leaf)) {
            vc = "***";
            vb = "***";
        }
        return R.PMG_W_SettingDiffers.Morfat(name, Abbreviate(vc), Abbreviate(vb));
    }

    const int MaxValueLength = 40;


    /// <summary>
    /// ﾌﾟﾛﾝﾌﾟﾄみたいな長文は1行目だけ切り詰めて出す
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    static string? Abbreviate(object? value) {
        var s = value?.ToString();
        if (s == null) {
            return null;
        }
        var line = s.Split('\n')[0].TrimEnd('\r');
        if (line.Length > MaxValueLength) {
            line = line[..MaxValueLength];
        }
        return line.Length < s.Length ? line + "…" : line;
    }
}
