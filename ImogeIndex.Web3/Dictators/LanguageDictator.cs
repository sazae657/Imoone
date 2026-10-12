using System.Globalization;

namespace ImogeIndex.Web3.Dictators;

/// <summary>
/// 言語の選択肢
/// </summary>
/// <param name="Code">ｶﾙﾁｬー名(空ならOS設定に従う)</param>
/// <param name="Name">表示名</param>
public sealed record LanguageChoice(string Code, string Name)
{
    public override string ToString() => Name;
}

/// <summary>
/// UI言語の切り替え(再起動で反映)
/// </summary>
public static class LanguageDictator
{
    /// <summary>
    /// OS設定に従う
    /// </summary>
    public const string NameOfSystemLanguage = "";

    /// <summary>
    /// 選択可能な言語のｺーﾄﾞ
    /// </summary>
    static readonly (string Code, string Name)[] SupportedLanguages = [
        ("ja", "日本語"),
        ("qps-ploc", "𓇌𓄿𓆓𓅱𓋔𓇌𓈖"),
    ];

    /// <summary>
    /// 選択肢全部(先頭はOS設定に従う)
    /// </summary>
    /// <returns></returns>
    public static IEnumerable<LanguageChoice> GetLanguageChoices()
    {
        yield return new LanguageChoice(NameOfSystemLanguage, Properties.Resources.LNG_System);
        foreach (var x in SupportedLanguages) {
            yield return new LanguageChoice(x.Code, x.Name);
        }
    }

    /// <summary>
    /// 内蔵言語に
    /// </summary>
    /// <param name="code"></param>
    /// <returns></returns>
    public static string Normalize(string? code)
    {
        if (string.IsNullOrEmpty(code)) {
            return NameOfSystemLanguage;
        }
        foreach (var x in SupportedLanguages) {
            if (string.Equals(x.Code, code, StringComparison.OrdinalIgnoreCase)) {
                return x.Code;
            }
        }
        // 知らないｺーﾄﾞはOS設定に従う事にしておく
        return NameOfSystemLanguage;
    }

    /// <summary>
    /// UIｶﾙﾁｬーを適用する(ﾘｿーｽを触る前に呼べ)
    /// </summary>
    /// <param name="code"></param>
    public static void Apply(string? code)
    {
        var normalized = Normalize(code);
        if (normalized == NameOfSystemLanguage) {
            return;
        }
        var culture = CultureInfo.GetCultureInfo(normalized);
        // ThreadPoolやTaskでも同じ言語になるよう既定値も変える
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        CultureInfo.CurrentUICulture = culture;
    }
}
