using ImogeIndex;
using ImogeIndex.Common;
using Tomlet;
using R = ImogeMerge.Properties.Resources;
namespace ImogeMerge;

/// <summary>
/// ﾌﾟﾛﾌｧｲﾙ名かﾃﾞｨﾚｸﾄﾘからﾌﾟﾛﾌｧｲﾙの場所を割り出す
/// </summary>
public static class ProfileInvestigator {
    const string EnvGroup = "Env";
    const string ImogeGroup = "IG";

    /// <summary>
    /// Profile.tomlの変数展開用
    /// </summary>
    sealed class ProfileVariables(DirectoryInfo root) : ICustomVariableJail {
        public bool Get(string group, string name, out string? val) {
            if (group.Equals(EnvGroup, StringComparison.OrdinalIgnoreCase)) {
                val = Environment.GetEnvironmentVariable(name);
                return val != null;
            }
            if (group.Equals(ImogeGroup, StringComparison.OrdinalIgnoreCase) && name == "CONFIG_ROOT") {
                val = root.FullName;
                return true;
            }
            val = null;
            return false;
        }
    }

    /// <summary>
    /// Profile.tomlのﾊﾟｽ
    /// </summary>
    public static FileInfo ProfileFile => SystemParams.SettingPouth.CombineFile(SystemParams.ProfilesOf);

    /// <summary>
    /// 解決
    /// </summary>
    /// <param name="spec">ﾌﾟﾛﾌｧｲﾙ名かﾃﾞｨﾚｸﾄﾘ</param>
    /// <returns></returns>
    /// <exception cref="DirectoryNotFoundException"></exception>
    public static DirectoryInfo Resolve(string spec) {
        // 設定ﾌｧｲﾙがあるﾃﾞｨﾚｸﾄﾘならそのまま使う
        var dir = new DirectoryInfo(spec);
        if (dir.Exists && dir.CombineFile(SystemParams.ConfigName).Exists) {
            return dir;
        }
        var pf = ProfileFile;
        if (pf.Exists) {
            var profile = TomletMain.To<Profile>(TomlParser.ParseFile(pf.FullName));
            var hit = profile.Profiles.FirstOrDefault(w => w.Key.Equals(spec, StringComparison.OrdinalIgnoreCase));
            if (hit.Value != null) {
                var root = pf.Directory!;
                var path = Path.GetFullPath(hit.Value.Path.ExpandCustomVariables(new ProfileVariables(root)), root.FullName);
                var rc = new DirectoryInfo(path);
                if (rc.Exists && rc.CombineFile(SystemParams.ConfigName).Exists) {
                    return rc;
                }
            }
        }
        throw new DirectoryNotFoundException(R.PMG_E_ProfileNotFound.Morfat(spec));
    }

    /// <summary>
    /// Profile.tomlに新しいﾌﾟﾛﾌｧｲﾙを登録
    /// </summary>
    /// <param name="name"></param>
    /// <param name="location"></param>
    /// <exception cref="InvalidOperationException"></exception>
    public static void Register(string name, DirectoryInfo location) {
        var pf = ProfileFile;
        var profile = LoadForRegister(name);
        // 設定ﾌｫﾙﾀﾞーの中なら既存のｴﾝﾄﾘに合わせて相対ﾊﾟｽにする
        var root = pf.Directory!.FullName;
        var relative = Path.GetRelativePath(root, location.FullName);
        var path = relative.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(relative)
            ? location.FullName : relative;
        profile.Profiles.Add(name, new ProfileItem { Path = path });
        var t = TomletMain.TomlStringFrom(profile, TomlSerializerOptions.Default);
        pf.OpenAndWriteText(t);
    }

    /// <summary>
    /// 登録できる名前か事前に確認
    /// </summary>
    /// <param name="name"></param>
    public static void EnsureRegistrable(string name) => LoadForRegister(name);

    static Profile LoadForRegister(string name) {
        var pf = ProfileFile;
        if (!pf.Exists) {
            throw new FileNotFoundException(R.PMG_E_DatumNotFound.Morfat(pf.FullName));
        }
        var profile = TomletMain.To<Profile>(TomlParser.ParseFile(pf.FullName));
        if (profile.Profiles.Keys.Any(w => w.Equals(name, StringComparison.OrdinalIgnoreCase))) {
            throw new InvalidOperationException(R.PMG_E_ProfileExists.Morfat(name));
        }
        return profile;
    }
}
