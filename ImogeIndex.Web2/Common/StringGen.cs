using System;
using System.Collections.Generic;
using System.Text;

namespace ImogeIndex.Web2.Common {
    public static class SurrogatePairTestCases {

        /// <summary>
        /// 問題起こしやすい文字列
        /// </summary>
        /// <returns></returns>
        public static Dictionary<string, string> GenerateProblematicStrings() {
            return new Dictionary<string, string>
            {
                // 正常なｻﾛｹﾞーﾄﾍﾟｱ(長さはchar単位で2、文字数としては1)
                ["Valid_SurrogatePair_Emoji"] = "𩸽",                // ﾎｯｹ (U+29E3D)
                ["Valid_SurrogatePair_Kanji"] = "𠮷野家",             // つちよし (U+20BB7) + 野家

                // 孤立したｻﾛｹﾞーﾄ(不正なUTF-16: Substring等で泣き別れになった状態)
                ["Invalid_HighSurrogateOnly"] = "Hello\uD83DWorld",   // 上位ｻﾛｹﾞーﾄのみ
                ["Invalid_LowSurrogateOnly"] = "Hello\uDE00World",   // 下位ｻﾛｹﾞーﾄのみ
                ["Invalid_ReversedSurrogate"] = "\uDE00\uD83D",       // 下位 -> 上位の逆順

                //  境界値(ｻﾛｹﾞーﾄ範囲の境界)
                ["Boundary_HighStart"] = "\uD800\uDC00",              // 最小ｻﾛｹﾞーﾄﾍﾟｱ (U+10000)
                ["Boundary_HighEnd"] = "\uDBFF\uDFFF",              // 最大ｻﾛｹﾞーﾄﾍﾟｱ (U+10FFFF)

                //  文字数制限(Substring / Truncate)でﾍﾟｱが破壊されやすい配置
                ["Truncate_Trap_5Chars"] = "abc𠮷def",                // ｲﾝﾃﾞｯｸｽ 3..4 がｻﾛｹﾞーﾄ(4文字で切り取るとｻﾛｹﾞーﾄが泣き別れる)

                // 結合文字・絵文字ｼーｹﾝｽ(ｻﾛｹﾞーﾄﾍﾟｱ + 制御文字の複合ｹーｽ)
                ["Complex_ZWJ_Family"] = "👨‍👩‍👧‍👦",                  // ZWJｼーｹﾝｽ (char長: 11)
                ["Complex_Variation"] = "葛\U000E0100",          // 異体字ｾﾚｸﾀ (IVS)
                ["Complex_Keycap"] = "1\uFE0F\u20E3",           // 囲み文字 (ｷーｷｬｯﾌﾟ)
                ["Complex_SkinTone"] = "👍🏽",                     // 基本絵文字 + ｽｷﾝﾄーﾝ修飾子
            };
        }

        /// <summary>
        /// 境界ﾄﾗｯﾌﾟ
        /// </summary>
        /// <param name="targetLength">切り詰めたい文字数(char単位)</param>
        public static string CreateSplitTrap(int targetLength) {
            if (targetLength < 1)
                throw new ArgumentOutOfRangeException(nameof(targetLength));

            // targetLength - 1 文字分の通常文字列 + ｻﾛｹﾞーﾄﾍﾟｱ
            // これを targetLength で Substring(0, targetLength) すると上位ｻﾛｹﾞーﾄだけが残る
            return new string('a', targetLength - 1) + "𠮷" + "tail";
        }
    }

    /// <summary>
    /// じぇね
    /// </summary>
    static class StringGen {
        static StringGen() {
            Problematics = SurrogatePairTestCases.GenerateProblematicStrings()
                .Where(w => !w.Key.StartsWith("Invalid_", StringComparison.Ordinal))
                .Select(w => w.Value).ToArray();
        }
        private static string[] Problematics;

        // UTF-16で長さ1の文字群(英数・記号・日本語)
        private static readonly char[] SingleChars =
            "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789シモーニタあいうえお爾子田!-_┣◤◢下广卞廿十亠卉与本二上旦上二本与卉亠十廿卞广下广卞廿十亠卉ล้้้".ToCharArray();

        // UTF-16で長さ2の絵文字群(単一ｻﾛｹﾞーﾄﾍﾟｱ)
        private static readonly string[] Emojis =
        [
            "💩", "♨", "🎉", "🍣", "🔥", "🚀", "🐱", "👀", "✨", "💡", "☕", "🍎", "👍", "🥳"
        ];

        /// <summary>
        /// 指定したLength(UTF-16ｺーﾄﾞﾕﾆｯﾄ数)の絵文字混入文字列を生成します。
        /// </summary>
        /// <param name="length">生成する文字列の長さ (string.Length)</param>
        /// <param name="emojiRatio">絵文字の出現率 (0.0 ～ 1.0)</param>
        public static string Generate(int length, double emojiRatio = 0.2) {
            if (length < 0) {
                throw new ArgumentOutOfRangeException(nameof(length), "文字数は0以上");
            }
            if (length == 0) {
                return string.Empty;
            }
            var sb = new StringBuilder(length);
            while (sb.Length < length) {
                int remaining = length - sb.Length;

                // 残りに収まる正常な文字列だけを追加する
                if (remaining >= 2 && Random.Shared.NextDouble() < emojiRatio) {
                    var candidate = Problematics[Random.Shared.Next(Problematics.Length)];
                    sb.Append(candidate.Length <= remaining ? candidate : SingleChars[Random.Shared.Next(SingleChars.Length)].ToString());
                }
                else {
                    // 残り1文字、または確率外なら1文字(長さ1)を追加
                    sb.Append(SingleChars[Random.Shared.Next(SingleChars.Length)]);
                }
            }

            return sb.ToString();
        }
    }

}
