using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ImogeIndex;
using ImogeIndex.Common;
using ImogeIndex.Web3.Dialogs;
using System.Windows;

namespace ImooneWPF.Modelle;

/// <summary>
/// ﾗﾍﾞﾙ編集
/// </summary>
public partial class LabelEditWindowModelle : ObservableValidator
{
    public LabelEditWindowModelle(ImogeIndex.Settings s) : base()
    {
        settings = s.UserDefinitions;
        if (s.UserDefinitions != null) {
            labels = string.Join(Environment.NewLine, s.UserDefinitions.Labels);
        }
    }

    ImogeIndex.UserDefinitions? settings;
    string labels = string.Empty;
    bool isDirty = false;

    [LabelOfList]
    public string Labels
    {
        get => labels;
        set
        {
            labels = value;
            try {
                var aliases = value.SplitNl().Select(MetaAlias.FromSettingure).ToList();
                MetaAlias.ValidateType(aliases);
            }
            catch { }
            isDirty = true;
            ValidateProperty(value);
        }
    }

    [RelayCommand]
    private async Task Ok(Window w)
    {
        if (!isDirty) {
            w.Close();
            return;
        }
        if (HasErrors) {
            return;
        }
        if (settings != null) {
            settings.Labels = labels.Trim().SplitNl().ToList();
            try {
                await settings.Jail();
            }
            catch (Exception r) {
                TaskenDialog.Error(w,
                    Properties.Resources.MC_ErrorError.Morfat(r.Message, r.StackTrace),
                    Properties.Resources.MC_Error);
            }
        }
        w.Close();
    }


    [RelayCommand]
    private void Cancel(Window w)
    {
        w.Close();
    }
}
