using ImogeIndex.Common;
using Tomlet;
using Tomlet.Attributes;

namespace ImogeIndex;

/// <summary>
/// ﾌﾟﾛﾌｵﾌﾞ中身
/// </summary>
public class ProfileItem
{
    public string Path { get; set; } = string.Empty;
}

/// <summary>
/// ﾌﾟﾛﾌ
/// </summary>
public class Profile : ICustomVariableJail {
    const string DefaultKey = "Default";
    const string EnvGroup = "Env";
    const string ImogeGroup = "IG";

    /// <summary>
    /// よみこみ
    /// </summary>
    public static void Load() {
        StringExtensions.Setup();
        var sm = SystemParams.CreateLocalStateFile(SystemParams.ProfilesOf).AsFileInfo();
        var tmp = new Profile();
        if (!sm.Exists) {
            tmp.SaveDefault();
        }
        tmp.SaveTemplate();

        var t = TomlParser.ParseFile(sm.FullName);
        instance = TomletMain.To<Profile>(t);
        instance.ProfilePath = sm;
        instance.Prepare(sm.Directory!);
    }

    /// <summary>
    /// 再読み込み
    /// </summary>
    public static void Reload() {
        instance.LoadSettings();
    }


    [TomlNonSerialized]
    static Profile instance = new Profile();
    [TomlNonSerialized]
    public static Profile Dictator => instance;

    [TomlNonSerialized]
    public string ActiveProfile { get; private set; } = DefaultKey;

    [TomlNonSerialized]
    public static PreparedSettings Active => instance.Settingure[instance.ActiveProfile];

    [TomlNonSerialized]
    public Dictionary<string, PreparedSettings> Settingure { get; } = new();

    [TomlNonSerialized]
    public FileInfo? ProfilePath { get; private set; }


    [TomlPrecedingComment("デフォルトプロファイル")]
    public string Default { get; set; } = string.Empty;

    [TomlPrecedingComment("プロファイル")]
    public Dictionary<string, ProfileItem> Profiles { get; set; } = new();

    /// <summary>
    /// ﾌﾟﾛﾌｧｲﾙ切り替え
    /// </summary>
    /// <param name="key"></param>
    /// <exception cref="Exception"></exception>
    public void SetActive(string key) {
        if (!Settingure.ContainsKey(key)) {
            throw new Exception(Properties.Resources.GL_E_UnknownProfile.Morfat(key));
        }
        ActiveProfile = key;
    }

    Dictionary<string, Dictionary<string, string>> envVars = new();

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
    /// 読み込みﾌﾟﾛﾌｧｲﾙの変数展開など
    /// </summary>
    /// <param name="root"></param>
    /// <exception cref="Exception"></exception>
    void Prepare(DirectoryInfo root) {
        envVars.Clear();
        envVars.Add(ImogeGroup,
            new Dictionary<string, string>()
            {
                    { "CONFIG_ROOT", root.FullName }
            });
        foreach (var item in Profiles) {
            var m = item.Value.Path.ExpandCustomVariables(this);
            item.Value.Path = Path.GetFullPath(m, root.FullName);
        }
        // ﾁｪｯｷﾝ
        if (string.IsNullOrWhiteSpace(Default)) {
            throw new Exception(Properties.Resources.GL_E_InvalidValue.Morfat(nameof(Default)));
        }
        if (!Profiles.ContainsKey(Default)) {
            throw new Exception(Properties.Resources.GL_E_ProfileNotInProfiles.Morfat(nameof(Default), Default, nameof(Profiles)));
        }
        ActiveProfile = Default;
        LoadSettings();
    }

    /// <summary>
    /// Settingsの全読み込み
    /// </summary>
    void LoadSettings() {
        foreach (var item in Profiles) {
            Settingure[item.Key] = Settings.Load(item.Key, item.Value.Path.AsDirectoryInfo());
        }
    }

    /// <summary>
    /// ﾃﾞﾌｫﾙﾄ値の保存
    /// </summary>
    void SaveDefault() {
        Default = DefaultKey;
        Profiles.Add(DefaultKey, new ProfileItem { Path = SystemParams.DefaultProfileLocation + Path.DirectorySeparatorChar + DefaultKey });
        var sm = SystemParams.CreateLocalStateFile(SystemParams.ProfilesOf).AsFileInfo();
        var t = TomletMain.TomlStringFrom(this, TomlSerializerOptions.Default);
        sm.OpenAndWriteText(t);
    }

    /// <summary>
    /// ﾃﾝﾌﾟﾚの保存
    /// </summary>
    void SaveTemplate() {
        Default = DefaultKey;
        Profiles.TryAdd(DefaultKey, new ProfileItem { Path = SystemParams.DefaultProfileLocation + Path.DirectorySeparatorChar + DefaultKey });
        var sm = SystemParams.CreateLocalStateFile(SystemParams.DefaultProfilesOf).AsFileInfo();
        var t = TomletMain.TomlStringFrom(this, TomlSerializerOptions.Default);
        sm.OpenAndWriteText(t);
    }
}
