using System.Buffers.Binary;
using System.Reflection;
using System.Text.RegularExpressions;

namespace ImogeIndex.Common;

/// <summary>
/// 変数展開
/// </summary>
public interface ICustomVariableJail {
    /// <summary>
    /// 変数展開
    /// </summary>
    /// <param name="group"></param>
    /// <param name="name"></param>
    /// <param name="val"></param>
    /// <returns></returns>
    bool Get(string group, string name, out string? val);
}

public static partial class StringExtensions {
    public static void Setup() {
    }

    [GeneratedRegex(@"\$\{(\w+):([^}]+)\}", RegexOptions.IgnoreCase)]
    private static partial Regex VarRefRegex();

    /// <summary>
    /// 文字列内の ${GROUP:VARIABLE_NAME} を変数に展開
    /// </summary>
    /// <param name="text"></param>
    /// <param name="vars"></param>
    /// <returns></returns>
    public static string ExpandCustomVariables(this string text, ICustomVariableJail vars) {
        if (string.IsNullOrEmpty(text)) {
            return text;
        }

        return VarRefRegex().Replace(text, match => {
            // 変数名を取得
            var group = match.Groups[1].Value;
            var varName = match.Groups[2].Value;
            if (varName == null) {
                return match.Value;
            }
            if (vars.Get(group, varName, out var cps)) {
                return cps ?? string.Empty;
            }
            return match.Value;
        });
    }

    /// <summary>
    /// ﾓー
    /// </summary>
    /// <param name="fmt"></param>
    /// <param name="args"></param>
    /// <returns></returns>
    public static string Morfat(this string fmt, params object?[] args) {
        return string.Format(fmt, args);
    }

    /// <summary>
    /// 正規表現に
    /// </summary>
    /// <param name="re"></param>
    /// <param name="opts"></param>
    /// <returns></returns>
    public static Regex AsRegex(this string re, RegexOptions opts = RegexOptions.Compiled) {
        return new Regex(re, RegexOptions.Compiled|opts, TimeSpan.FromSeconds(1));
    }

    /// <summary>
    /// 正規表現に(IG)
    /// </summary>
    /// <param name="re"></param>
    /// <returns></returns>
    public static Regex AsRegexIgnoreCase(this string re)
    {
        return new Regex(re, RegexOptions.Compiled| RegexOptions.IgnoreCase, TimeSpan.FromSeconds(1));
    }

    [GeneratedRegex(@"\r?\n", RegexOptions.IgnoreCase)]
    private static partial Regex NL();

    /// <summary>
    /// 改行区切り
    /// </summary>
    /// <param name="txt"></param>
    /// <returns></returns>
    public static IEnumerable<string> SplitNl(this string txt) {
        return NL().Split(txt).Select(w => w.Trim()).Where(w => !string.IsNullOrEmpty(w));
    }

    /// <summary>
    /// ｶﾝﾏくぎり
    /// </summary>
    /// <param name="txt"></param>
    /// <returns></returns>
    public static IEnumerable<string> SplitCsv(this string txt)
    {
        return txt.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(w => w.Trim());
    }

    /// <summary>
    /// ｾﾐｺﾛﾝくぎり
    /// </summary>
    /// <param name="txt"></param>
    /// <returns></returns>
    public static IEnumerable<string> SplitSmc(this string txt) {
        return txt.Split(';', StringSplitOptions.RemoveEmptyEntries).Select(w => w.Trim()).Where(w => !string.IsNullOrEmpty(w));
    }

    [GeneratedRegex(@"\r?\n|[;,]", RegexOptions.IgnoreCase)]
    private static partial Regex NCS();

    /// <summary>
    /// ぜんぶ
    /// </summary>
    /// <param name="txt"></param>
    /// <returns></returns>
    public static IEnumerable<string> SplitNCS(this string txt) {
        return NCS().Split(txt).Select(w => w.Trim()).Where(w => !string.IsNullOrEmpty(w));
    }

    [GeneratedRegex(@"[\s\t\u3000]", RegexOptions.IgnoreCase)]
    private static partial Regex WSP();

    /// <summary>
    /// 空白有るﾊﾟｽを囲む
    /// </summary>
    /// <param name="path"></param>
    /// <returns></returns>
    public static string AutoQuotePath(this string path)
    {
        if (!WSP().IsMatch(path)) {
            return path;
        }
        return "\"" + path + "\"";
    }
}


