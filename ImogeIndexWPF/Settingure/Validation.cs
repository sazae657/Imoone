using ImogeIndex;
using ImogeIndex.Common;
using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace ImogeIndexWPF.Settingure;

public enum MurArraySeparator
{
    NewLine, Comma, Semicolon, Loose
}
/// <summary>
/// 正規表現の配列のやつ
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class RegexArrayAttribute : ValidationAttribute
{
    MurArraySeparator separator = MurArraySeparator.NewLine;
    public RegexArrayAttribute(MurArraySeparator sep)
    {
        separator = sep;
    }

    public RegexArrayAttribute(string massage, MurArraySeparator sep)
    {
        if (!string.IsNullOrEmpty(massage)) {
            ErrorMessage = massage;
        }
        separator = sep;
    }

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        var input = value?.ToString() ?? string.Empty;
        if (string.IsNullOrEmpty(input)) {
            return ValidationResult.Success;
        }
        foreach (var x in separator switch {
            MurArraySeparator.NewLine => input.SplitNl(),
            MurArraySeparator.Comma => input.SplitCsv(),
            MurArraySeparator.Semicolon => input.SplitSmc(),
            MurArraySeparator.Loose => input.SplitNCS(),
            _ => throw new NotImplementedException(separator.ToString())}
        )
        {
            try {
                new Regex(x, RegexOptions.IgnoreCase, TimeSpan.FromSeconds(1));
            }
            catch (Exception ex) {
                ErrorMessage = ex.Message;
                return new ValidationResult(ex.Message);
            }
        }
        return ValidationResult.Success;
    }
}

/// <summary>
/// ｽｯﾄﾞﾚ数
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class ThreadBegrenzungAttribute : ValidationAttribute {
    public ThreadBegrenzungAttribute() {
    }

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext) {
        if (value is not int volume) {
            ErrorMessage = "value != int";
            return new ValidationResult("value != int");
        }
        ErrorMessage = Properties.Resources.SG_E_InvalidThreadNum;
        return volume >= 1 ? ValidationResult.Success : new ValidationResult(ErrorMessage);
    }
}

/// <summary>
/// 雑URI
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class LooseUriAttribute : ValidationAttribute {

    public LooseUriAttribute() {
    }

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext) {
        if (value is not string volume) {
            ErrorMessage = "value != string";
            return new ValidationResult("value != string");
        }
        if (string.IsNullOrEmpty(volume)) {
            return ValidationResult.Success;
        }
        try {
            new Uri(volume);
        }
        catch (Exception ex) {
            ErrorMessage = ex.Message;
            return new ValidationResult(ex.Message);
        }
        return ValidationResult.Success;
    }
}


[AttributeUsage(AttributeTargets.Property)]
public sealed class LooseMetaAliasAttribute : ValidationAttribute {

    static readonly Regex InvalidChars =new (@"[:;=""'\|]", RegexOptions.Compiled, TimeSpan.FromSeconds(1));

    public LooseMetaAliasAttribute() {
    }
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext) {
        if (value is not string volume || string.IsNullOrEmpty(volume)) {
            return ValidationResult.Success;
        }
        var meta = new HashSet<MetaAlias>();
        foreach (var k in volume.SplitNl()) {
            try {
                var m = MetaAlias.FromSettingure(k);
                MetaAlias.ValidateType(meta.Append(m));
                if (!meta.Add(m)) {
                    ErrorMessage = Properties.Resources.SG_E_DuplicateMetaAlias.Morfat(m.ToSettingure());
                    return new ValidationResult(ErrorMessage);
                }
                var mc = InvalidChars.Match(m.Alias);
                if (mc.Success) {
                    ErrorMessage = Properties.Resources.SG_E_InvalidCharOnMetaAlias.Morfat(m.ToSettingure());
                    return new ValidationResult(ErrorMessage);
                }
            }
            catch (Exception r){
                ErrorMessage = r.Message;
                return new ValidationResult(r.Message);
            }
        }

        return ValidationResult.Success;
    }
}
