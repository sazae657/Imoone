using ImogeIndex.Common;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace ImogeIndex.Captionizers;

/// <summary>
/// ｷｬﾌﾟｼｮﾝ訂正
/// </summary>
internal static class CaptionErupter
{
    static class Eruptings
    {
        // 先頭の不正文字
        public static readonly Regex InvalidStatChars = new Regex("\\A[\'\"`]+", RegexOptions.Compiled, TimeSpan.FromSeconds(1));
        // ｹﾂの不正文字
        public static readonly Regex InvalidEndChars = new Regex("[`]+\\Z", RegexOptions.Compiled, TimeSpan.FromSeconds(1));
        // 行全体が囲われている
        public static readonly Regex ExcesoQuote = new Regex("\\A[\"'](?<Content>.*)[\"']\\Z", RegexOptions.Compiled, TimeSpan.FromSeconds(1));

        // 存在してはならない "
        public static readonly Regex InvalidQuote = new Regex("(\\])(?<Quote>[\"])\\Z", RegexOptions.Compiled, TimeSpan.FromSeconds(1));
        // なんかQuoteされてる数字
        public static readonly Regex QuotedNumeric = new Regex("['\"](?<Num>[0-9]+)['\"]", RegexOptions.Compiled, TimeSpan.FromSeconds(1));
        // [details] などのﾃーﾌﾞﾙﾍｯﾀﾞ
        public static readonly Regex TableHeader = new Regex("\\A\\[[A-Za-z0-9_.]+\\]\\Z", RegexOptions.Compiled, TimeSpan.FromSeconds(1));
    }

    enum ValueType
    {
        Unko, Numeric, String, StringList
    }
    class Schema
    {
        public string Key { get; set; }
        public ValueType Type { get; set; }

        public Schema(string key, ValueType type)
        {
            Key = key;
            Type = type;
        }
    }

    static readonly Schema[] schema = [
        new("time_of_day", ValueType.String),
        new("dominant_color_palette", ValueType.String),
        new("people_count", ValueType.Numeric),
        new("keywords", ValueType.StringList),
        new("details.scene", ValueType.String),
        new("details.subjects", ValueType.String),
        new("details.action", ValueType.String),
        new("details.mood", ValueType.String),
        new("details.shot_type", ValueType.String),
        new("details.impression", ValueType.String),
        new("details.costume", ValueType.StringList),
        new("details.objects", ValueType.StringList),
        new("details.use_cases", ValueType.StringList),
    ];

    static readonly Dictionary<string, ValueType> typeMap =
        [with(schema.Select(w => new KeyValuePair<string, ValueType>(w.Key, w.Type)))];

    /// <summary>
    /// ValueTypeを類推
    /// </summary>
    /// <param name="str"></param>
    /// <returns></returns>
    static ValueType EstimateValueType(string str, ValueType hint)
    {
        if (str.StartsWith('[') && str.EndsWith(']')) {
            return ValueType.StringList;
        }
        if ((str.StartsWith('"') || str.StartsWith('\'')) &&
            (str.EndsWith('"') || str.EndsWith('\''))) {
            if (hint == ValueType.Numeric && Eruptings.QuotedNumeric.IsMatch(str)) {
                return ValueType.Numeric;
            }
            return ValueType.String;
        }
        if (int.TryParse(str, out _)) {
            return ValueType.Numeric;
        }
        return ValueType.Unko;
    }

    /// <summary>
    /// 行単位で変な行を訂正できるだけ訂正
    /// </summary>
    /// <param name="toml"></param>
    /// <returns></returns>
    public static string Process(string toml)
    {
        var rs = new StringBuilder();
        // 次に来るはずのｽｷーﾏ位置
        var pos = 0;
        foreach (var x in toml.SplitNl()) {
            var line = x;

            // ﾃーﾌﾞﾙﾍｯﾀﾞ混じりは位置推定が成り立たないので諦める
            if (Eruptings.TableHeader.IsMatch(line)) {
                return toml;
            }

            // ｹﾂの不正文字
            var m = Eruptings.InvalidEndChars.Match(line);
            if (m.Success) {
                line = line.Substring(0, m.Index);
            }

            if (line.IndexOf('=') < 0) {
                if (pos >= schema.Length) {
                    return toml;
                }
                var vdss = schema[pos];
                // Gemmaとかで頑なにｷーを付けないﾊﾟﾀーﾝがあるので類推
                var vt = EstimateValueType(line, vdss.Type);
                if (vt != vdss.Type) {
                    return toml; // ちょっと壊れすぎ
                }
                // ｷー付与
                line = line.Insert(0, $"{vdss.Key} = ");
            }

            m = Eruptings.ExcesoQuote.Match(line);
            if (m.Success) {
                line = m.Groups["Content"].Value;
            }

            // 先頭の不正文字
            m = Eruptings.InvalidStatChars.Match(line);
            if (m.Success) {
                line = line.Substring(m.Index + m.Length);
            }


            var si = line.IndexOf('=');
            var key = line.Substring(0, si).Trim();
            var value = line.Substring(si+1).Trim();
            bool modified = false;
            // ｷー付き行で位置を同期し、欠落行があっても後続のｷー推定がずれないようにする
            var schemaIndex = Array.FindIndex(schema, w => w.Key == key);
            pos = schemaIndex >= 0 ? schemaIndex + 1 : pos + 1;
            if (typeMap.TryGetValue(key, out var type)) {
                if (type == ValueType.StringList) {
                    // 存在してはならない "
                    m = Eruptings.InvalidQuote.Match(value);
                    if (m.Success) {
                        value = value.Remove(m.Groups["Quote"].Index, m.Groups["Quote"].Length);
                        modified = true;
                    }
                }
                if (type == ValueType.Numeric) {
                    m = Eruptings.QuotedNumeric.Match(value);
                    if (m.Success) {
                        value = m.Groups["Num"].Value;
                        modified = true;
                    }
                }

                if (type == ValueType.String) {
                    // 既存のｸｫーﾄに合わせて対になるよう補う(無ければ " )
                    var q = value.StartsWith('\'') && !value.EndsWith('"') ? '\''
                          : value.EndsWith('\'') && !value.StartsWith('"') ? '\''
                          : '"';
                    if (!value.StartsWith(q)) {
                        value = q + value;
                        modified = true;
                    }
                    if (value.Length < 2 || !value.EndsWith(q)) {
                        value += q;
                        modified = true;
                    }
                }
            }
            if (modified) {
                line = $"{key} = {value}";
            }

            if (line[(line.IndexOf('=') + 1)..].TrimStart().StartsWith('[') && !line.EndsWith(']')) {
                line += "]";
            }
            rs.Append(line).Append('\n');
        }
        return rs.ToString();
    }
}
