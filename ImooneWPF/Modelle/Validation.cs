using ImogeIndex.Common;
using System.ComponentModel.DataAnnotations;

namespace ImooneWPF.Modelle;

[AttributeUsage(AttributeTargets.Property)]
public sealed class LabelOfListAttribute : ValidationAttribute
{

    public LabelOfListAttribute()
    {
    }

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is not string volume || string.IsNullOrEmpty(volume)) {
            return ValidationResult.Success;
        }
        var vour = new HashSet<string>();
        foreach (var k in volume.SplitNl()) {
            try {
                if (!vour.Add(k)) {
                    ErrorMessage = Properties.Resources.LE_E_DuplicateLabel.Morfat(k);
                    return new ValidationResult(ErrorMessage);
                }
            }
            catch (Exception r) {
                ErrorMessage = r.Message;
                return new ValidationResult(r.Message);
            }
        }

        return ValidationResult.Success;
    }
}
