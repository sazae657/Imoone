using System.Text.RegularExpressions;
using ImogeIndex.Common;

namespace ImogeIndex.Web2;

/// <summary>
/// StringExtensions
/// </summary>
public class StringExtensionsController
{
    /// <summary>
    /// 変数展開ｽﾀﾌﾞ
    /// </summary>
    private sealed class FakeJail : ICustomVariableJail
    {
        private readonly Dictionary<string, string?> table;

        public FakeJail(Dictionary<string, string?> table) => this.table = table;

        public bool Get(string group, string name, out string? val)
            => table.TryGetValue($"{group}:{name}", out val);
    }

    /// <summary>
    /// 呼び出されたかどうかだけを記録するｽﾀﾌﾞ
    /// </summary>
    private sealed class CountingJail : ICustomVariableJail
    {
        private readonly Action onGet;

        public CountingJail(Action onGet) => this.onGet = onGet;

        public bool Get(string group, string name, out string? val)
        {
            onGet();
            val = null;
            return false;
        }
    }

    private static FakeJail Jail(params (string Key, string? Value)[] entries)
        => new(entries.ToDictionary(e => e.Key, e => e.Value));

    #region ExpandCustomVariables

    [Fact(DisplayName = "単一の変数を展開")]
    public void ExpandCustomVariables1()
    {
        var jail = Jail(("ENV:NAME", "imoge"));
        Assert.Equal("hello imoge!", "hello ${ENV:NAME}!".ExpandCustomVariables(jail));
    }

    [Fact(DisplayName = "複数の変数を展開")]
    public void ExpandCustomVariables2()
    {
        var jail = Jail(("A:X", "1"), ("B:Y", "2"));
        Assert.Equal("1-2", "${A:X}-${B:Y}".ExpandCustomVariables(jail));
    }

    [Fact(DisplayName = "同じ変数が繰り返されても全て展開")]
    public void ExpandCustomVariables3()
    {
        var jail = Jail(("A:X", "z"));
        Assert.Equal("zzz", "${A:X}${A:X}${A:X}".ExpandCustomVariables(jail));
    }

    [Fact(DisplayName = "未定義の変数は元のまま残す")]
    public void ExpandCustomVariables4()
    {
        var jail = Jail(("A:X", "1"));
        Assert.Equal("1/${A:UNKNOWN}", "${A:X}/${A:UNKNOWN}".ExpandCustomVariables(jail));
    }

    [Fact(DisplayName = "値がnullなら空文字に置換する")]
    public void ExpandCustomVariables5()
    {
        var jail = Jail(("A:X", null));
        Assert.Equal("[]", "[${A:X}]".ExpandCustomVariables(jail));
    }

    [Fact(DisplayName = "変数名にコロン以外の記号を含められる")]
    public void ExpandCustomVariables6()
    {
        var jail = Jail(("A:a/b.c♨", "v"));
        Assert.Equal("v", "${A:a/b.c♨}".ExpandCustomVariables(jail));
    }

    [Theory(DisplayName = "展開対象が無ければ入力をそのまま返す")]
    [InlineData("")]
    [InlineData("♨変数のない普通の文字列♨")]
    [InlineData("${MALFORMED")]
    [InlineData("${NOCOLON}")]
    [InlineData("${:NOGROUP}")]
    [InlineData("$ {A:X}")]
    public void ExpandCustomVariables7(string text)
    {
        var jail = Jail(("A:X", "1"));
        Assert.Equal(text, text.ExpandCustomVariables(jail));
    }

    [Fact(DisplayName = "展開結果に含まれる変数表記は再展開しない")]
    public void ExpandCustomVariables8()
    {
        var jail = Jail(("A:X", "${A:Y}"), ("A:Y", "final"));
        Assert.Equal("${A:Y}", "${A:X}".ExpandCustomVariables(jail));
    }

    [Fact(DisplayName = "空文字列ならJailを呼ばない")]
    public void ExpandCustomVariables9()
    {
        var called = false;
        var jail = new CountingJail(() => called = true);
        Assert.Equal(string.Empty, string.Empty.ExpandCustomVariables(jail));
        Assert.False(called);
    }

    #endregion

    #region Morfat

    [Fact(DisplayName = "書式に引数を埋め込む")]
    public void Morfat1()
    {
        Assert.Equal("a=1, b=x", "a={0}, b={1}".Morfat(1, "x"));
    }

    [Fact(DisplayName = "引数が無ければ書式をそのまま返す")]
    public void Morfat2()
    {
        Assert.Equal("プレーン", "プレーン".Morfat());
    }

    [Fact(DisplayName = "null要素は空文字になる")]
    public void Morfat3()
    {
        Assert.Equal("[]", "[{0}]".Morfat((object?)null));
    }

    [Fact(DisplayName = "配列そのものがnullなら例外")]
    public void Morfat4()
    {
        Assert.Throws<ArgumentNullException>(() => "[{0}]".Morfat(null!));
    }

    [Fact(DisplayName = "同じ引数を複数回参照できる")]
    public void Morfat5()
    {
        Assert.Equal("1-1", "{0}-{0}".Morfat(1));
    }

    [Fact(DisplayName= "引数が不足していれば例外")]
    public void Morfat6()
    {
        Assert.Throws<FormatException>(() => "{0}{1}".Morfat(1));
    }

    #endregion

    #region AsRegex / AsRegexIgnoreCase

    [Fact(DisplayName = "正規表現")]
    public void AsRegex1()
    {
        var re = @"^\d{3}-\d{4}$".AsRegex();
        Assert.Matches(re, "123-4567");
        Assert.DoesNotMatch(re, "12-4567");
    }

    [Fact(DisplayName = "既定では大文字小文字を区別する")]
    public void AsRegex2()
    {
        Assert.DoesNotMatch("abc".AsRegex(), "ABC");
    }

    [Fact(DisplayName = "オプションを追加指定できる")]
    public void AsRegex3()
    {
        var re = "abc".AsRegex(RegexOptions.IgnoreCase);
        Assert.Matches(re, "ABC");
        Assert.True(re.Options.HasFlag(RegexOptions.IgnoreCase));
    }

    [Fact(DisplayName = "Compiledとタイムアウトが常に付与される")]
    public void AsRegex4()
    {
        var re = "x".AsRegex();
        Assert.True(re.Options.HasFlag(RegexOptions.Compiled));
        Assert.Equal(TimeSpan.FromSeconds(1), re.MatchTimeout);
    }

    [Fact(DisplayName = "不正なパターンは例外")]
    public void AsRegex5()
    {
        Assert.Throws<RegexParseException>(() => "([".AsRegex());
    }

    [Fact(DisplayName = "大文字小文字を無視する")]
    public void AsRegexIgnoreCase6()
    {
        var re = "ImoGe".AsRegexIgnoreCase();
        Assert.Matches(re, "imoge");
        Assert.Matches(re, "IMOGE");
    }

    [Fact(DisplayName = "Compiledとタイムアウトが付与される")]
    public void AsRegexIgnoreCase7()
    {
        var re = "x".AsRegexIgnoreCase();
        Assert.True(re.Options.HasFlag(RegexOptions.Compiled));
        Assert.True(re.Options.HasFlag(RegexOptions.IgnoreCase));
        Assert.Equal(TimeSpan.FromSeconds(1), re.MatchTimeout);
    }

    #endregion

    #region SplitNl

    [Fact(DisplayName = "LFで分割する")]
    public void SplitNl1()
    {
        Assert.Equal(["a", "b", "c"], "a\nb\nc".SplitNl());
    }

    [Fact(DisplayName = "CrLfで分割する")]
    public void SplitNl2()
    {
        Assert.Equal(["a", "b", "c"], "a\r\nb\r\nc".SplitNl());
    }

    [Fact(DisplayName = "各要素をトリムする")]
    public void SplitNl3()
    {
        Assert.Equal(["a", "b"], "  a  \n\tb\t".SplitNl());
    }

    [Fact(DisplayName = "空行と空白のみの行を除去する")]
    public void SplitNl4()
    {
        Assert.Equal(["a", "b"], "a\n\n   \r\n\r\nb\n".SplitNl());
    }

    [Fact(DisplayName = "改行が無ければ一要素になる")]
    public void SplitNl5()
    {
        Assert.Equal(["single"], " single ".SplitNl());
    }

    [Fact(DisplayName = "空文字列なら空になる")]
    public void SplitNl6()
    {
        Assert.Empty("".SplitNl());
        Assert.Empty("\r\n\r\n".SplitNl());
    }

    #endregion

    #region SplitCsv

    [Fact(DisplayName = "カンマで分割する")]
    public void SplitCsv1()
    {
        Assert.Equal(["a", "b", "c"], "a,b,c".SplitCsv());
    }

    [Fact(DisplayName = "空要素を除去する")]
    public void SplitCsv2()
    {
        Assert.Equal(["a", "b"], ",a,,b,".SplitCsv());
    }

    [Fact(DisplayName = "前後の空白はトリムする")]
    public void SplitCsv3()
    {
        Assert.Equal(["a", "b"], " a , b".SplitCsv());
    }

    [Fact(DisplayName = "カンマが無ければ一要素になる")]
    public void SplitCsv4()
    {
        Assert.Equal(["abc"], "abc".SplitCsv());
    }

    [Fact(DisplayName = "空文字列なら空になる")]
    public void SplitCsv5()
    {
        Assert.Empty("".SplitCsv());
        Assert.Empty(",,,".SplitCsv());
    }

    #endregion

    #region SplitSmc

    [Fact(DisplayName = "セミコロンで分割する")]
    public void SplitSmc1()
    {
        Assert.Equal(["a", "b", "c"], "a;b;c".SplitSmc());
    }

    [Fact(DisplayName = "各要素をトリムする")]
    public void SplitSmc2()
    {
        Assert.Equal(["a", "b"], " a ; b ".SplitSmc());
    }

    [Fact(DisplayName = "空要素を除去する")]
    public void SplitSmc3()
    {
        Assert.Equal(["a", "b"], ";a;;b;".SplitSmc());
    }

    [Fact(DisplayName = "空白のみの要素は消える")]
    public void SplitSmc4()
    {
        Assert.Equal(["a", "b"], "a; ;b".SplitSmc());
    }

    [Fact(DisplayName = "空文字列なら空になる")]
    public void SplitSmc5()
    {
        Assert.Empty("".SplitSmc());
        Assert.Empty(";;;".SplitSmc());
    }

    #endregion

    #region SplitNCS
    [Fact(DisplayName = "混在で分割する")]
    public void SplitNCS1() {
        Assert.Equal(["a", "b", "c", "d", "e"], "a;b,c\nd\r\ne".SplitNCS());
    }

    [Fact(DisplayName = "混在で各要素をトリムする")]
    public void SplitNCS2() {
        Assert.Equal(["a", "b", "c", "d", "e"], "a ; b , c \n d \r\n e".SplitNCS());
    }

    [Fact(DisplayName = "混在で空要素を除去する")]
    public void SplitNCS3() {
        Assert.Equal(["a", "b"], ",a;;\r\n,;b;\n".SplitNCS());
    }

    [Fact(DisplayName = "混在で空白のみの要素は消える")]
    public void SplitNCS4() {
        Assert.Equal(["a", "b"], "a\r\n , ;b".SplitNCS());
    }

    [Fact(DisplayName = "混在で空文字列なら空になる")]
    public void SplitNCS5() {
        Assert.Empty("".SplitNCS());
        Assert.Empty(";,\r\n\n".SplitNCS());
    }

    #endregion
}
