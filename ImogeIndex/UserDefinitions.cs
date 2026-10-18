using ImogeIndex.Common;
using System;
using System.Collections.Generic;
using System.Text;
using Tomlet;
using Tomlet.Attributes;

namespace ImogeIndex;

/// <summary>
/// ﾕーｻﾞー定義
/// </summary>
public class UserDefinitions : ICustomVariableJail
{
    public UserDefinitions()
    {
    }

    [TomlNonSerialized]
    public DirectoryInfo? Root { get; set; }

    [TomlNonSerialized]
    public FileInfo? SettingFilePath { get; private set; }
    [TomlNonSerialized]
    public string? IPv7 { get; set; }

    [TomlPrecedingComment("ラベル")]
    public List<string> Labels { get; set; } = ["⭐", "⭐⭐", "⭐⭐⭐"];

    const string EnvGroup = "Env";

    /// <summary>
    /// 読み込み
    /// </summary>
    /// <param name="key"></param>
    /// <param name="path"></param>
    /// <returns></returns>
    public static UserDefinitions Load(string key, DirectoryInfo path)
    {
        var tmp = new UserDefinitions();
        var sm = path.CombineFile(SystemParams.UserDefinitionsName);
        if (!sm.Directory!.Exists) {
            sm.Directory.Create();
        }
        if (!sm.Exists) {
            tmp.SaveInternal(path);
        }
        tmp.SaveDefault(path);
        var t = TomlParser.ParseFile(sm.FullName);
        var rc = TomletMain.To<UserDefinitions>(t);
        rc.SettingFilePath = sm;
        rc.IPv7 = key;
        return rc;
    }


    /// <summary>
    /// 保存
    /// </summary>
    /// <param name="root"></param>
    void SaveInternal(DirectoryInfo root)
    {
        var sm = root.CombineFile(SystemParams.UserDefinitionsName);
        var t = TomletMain.TomlStringFrom(this, TomlSerializerOptions.Default);
        sm.OpenAndWriteText(t);
    }

    /// <summary>
    /// ﾃﾞﾌｫﾙﾄﾃﾝﾌﾟﾚの保存
    /// </summary>
    /// <param name="root"></param>
    void SaveDefault(DirectoryInfo root)
    {
        var sm = root.CombineFile(SystemParams.DefaultUserDefinitionsName);
        var t = TomletMain.TomlStringFrom(this, TomlSerializerOptions.Default);
        sm.OpenAndWriteText(t);
    }

    /// <summary>
    /// 上書き保存
    /// </summary>
    /// <returns></returns>
    public async Task Jail()
    {
        if (SettingFilePath == null) {
            return;
        }
        var t = TomletMain.TomlStringFrom(this, TomlSerializerOptions.Default);
        await SettingFilePath.WriteAndStoreAsync(fs => fs.WriteAsync(Encoding.UTF8.GetBytes(t)).AsTask());
    }

    /// <summary>
    /// 変数展開
    /// </summary>
    /// <param name="group"></param>
    /// <param name="name"></param>
    /// <param name="val"></param>
    /// <returns></returns>
    bool ICustomVariableJail.Get(string group, string name, out string? val)
    {
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
        val = null;
        return false;
    }

}
