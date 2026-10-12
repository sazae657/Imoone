using System.Text.RegularExpressions;

namespace ImogeIndex.Common;


/// <summary>
/// 共通
/// </summary>
static partial class CommonBackspaceEnvironment {
    /// <summary>
    /// ｳﾞｧー
    /// </summary>
    public const char Separator = '\b';

    /// <summary>
    /// 改行置き換えｺーﾄﾞ
    /// </summary>
    const string Spacement = "\u007F";

    /// <summary>
    /// 世界標準の改行(Macintoshは知らん、滅べ)
    /// </summary>
    const string LineBreak = "\r\n";

    /// <summary>
    /// 改行を引っかける(Macintoshは知らん、滅べ)
    /// </summary>
    /// <returns></returns>
    [GeneratedRegex(@"\r\n|\r|\n")]
    private static partial Regex NL();

    /// <summary>
    ///ｴｽｹーﾌﾟ(Macintoshは知らん、滅べ)
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    public static string Escape(this string input) {
        return NL().Replace(input, Spacement);
    }

    /// <summary>
    /// ｴｽｹーﾌﾟ解除(Macintoshは知らん、滅べ)
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    public static string Unescape(this string input) {
        return input.Replace(Spacement, LineBreak);
    }
}

/// <summary>
/// BSVの中身
/// </summary>
public interface IBSVContainer {
    /// <summary>
    /// 文字列から構築
    /// </summary>
    /// <param name="src"></param>
    /// <returns></returns>
    bool TryParse(string src);

    /// <summary>
    /// BSVに
    /// </summary>
    /// <returns></returns>
    string ToBSV();
}

/// <summary>
/// KV形式BSV
/// </summary>
public class BSVPair : IBSVContainer, IEquatable<BSVPair>
{
    /// <summary>
    /// ｷー
    /// </summary>
    public string? Key { get; set; }

    /// <summary>
    /// 値
    /// </summary>
    public string? Value { get; set; }

    public BSVPair() { }

    /// <summary>
    /// ﾀー
    /// </summary>
    /// <param name="key"></param>
    /// <param name="value"></param>
    public BSVPair(string key, string value)
    {
        Key = key;
        Value = value;
    }

    /// <summary>
    /// 文字列から構築
    /// </summary>
    /// <param name="src"></param>
    /// <returns></returns>
    public bool TryParse(string line) {
        var sp = line.Split([CommonBackspaceEnvironment.Separator]);
        if (sp.Length != 2) {
            return false;
        }
        Key = sp[0].Unescape();
        Value = sp[1].Unescape();
        return true;
    }

    /// <summary>
    /// BSVに
    /// </summary>
    /// <returns></returns>
    public string ToBSV() {
        if (string.IsNullOrEmpty(Value)) {
            if (!string.IsNullOrEmpty(Key) && Key.StartsWith('#')) {
                return Key.Escape(); // ｺﾒﾝﾄ行として出力
            }
        }
        return $"{Key?.Escape()}{CommonBackspaceEnvironment.Separator}{Value?.Escape()}";
    }

    #region IEquatable

    public bool Equals(BSVPair? other)
    {
        if (ReferenceEquals(this, other)) {
            return true;
        }
        if (other is null) {
            return false;
        }
        return string.Equals(Key, other.Key) &&
            string.Equals(Value, other.Value);
    }

    public override bool Equals(object? obj) => obj is BSVPair other && Equals(other);

    public override int GetHashCode() =>
        (Key is null ? 0 : StringComparer.Ordinal.GetHashCode(Key)) ^ (Value is null ? 0 : StringComparer.Ordinal.GetHashCode(Value));
    #endregion
}

/// <summary>
/// 可変長BSV
/// </summary>
public class BSVRow : IBSVContainer, IEquatable<BSVRow> {
    /// <summary>
    /// 空
    /// </summary>
    static readonly string[] EmptyRule = Array.Empty<string>();

    /// <summary>
    /// あたい
    /// </summary>
    public string[]? Values { get; set; }

    /// <summary>
    /// 長さ
    /// </summary>
    public int Length => Values?.Length ?? 0;


    public BSVRow() {
    }

    /// <summary>
    /// ﾀー
    /// </summary>
    /// <param name="src"></param>
    public BSVRow(params string[] src) {
        Values = src;
    }

    /// <summary>
    /// ﾀー
    /// </summary>
    /// <param name="first"></param>
    /// <param name="values"></param>
    public BSVRow(string first, IEnumerable<string> values)
    {
        var smp = new List<string> {
            first
        };
        Values = smp.Concat(values).ToArray();
    }

    /// <summary>
    /// ﾀー
    /// </summary>
    /// <param name="src"></param>
    public BSVRow(IEnumerable<string> src) {
        Values = [.. src];
    }

    /// <summary>
    /// 文字列から構築
    /// </summary>
    /// <param name="src"></param>
    /// <returns></returns>
    public bool TryParse(string line) {
        Values = [.. line.Split([CommonBackspaceEnvironment.Separator]).Select(w => w.Unescape())];
        return true;
    }

    /// <summary>
    /// BSVに
    /// </summary>
    /// <returns></returns>
    public string ToBSV() {
        return string.Join(CommonBackspaceEnvironment.Separator, Values?.Select(w => w.Escape()) ?? EmptyRule);
    }

    #region IEquatable

    public bool Equals(BSVRow? other) {
        if (ReferenceEquals(this, other)) {
            return true;
        }
        if (other is null) {
            return false;
        }
        if (Values is null) {
            return other.Values is null;
        }
        if (other.Values is null || Values.Length != other.Values.Length) {
            return false;
        }
        return Values.SequenceEqual(other.Values);
    }

    public override bool Equals(object? obj) => obj is BSVRow other && Equals(other);

    public override int GetHashCode() {
        if (Values is null) {
            return 0;
        }
        var hash = new HashCode();
        foreach (var v in Values) {
            hash.Add(v, StringComparer.Ordinal);
        }
        return hash.ToHashCode();
    }
    #endregion
}
