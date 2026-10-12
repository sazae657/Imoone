using CommunityToolkit.Mvvm.ComponentModel;
using ImogeIndex;
using ImogeIndex.Common;
using ImogeIndexWPF.Settingure.Dictators;
using System.ComponentModel.DataAnnotations;

namespace ImogeIndexWPF.Settingure.Modelle.Pages;

/// <summary>
/// ｷｬﾌﾟｼｮﾆﾝｸﾞ全般
/// </summary>
internal sealed partial class CaptioningPageModell : PageModell
{
    public CaptioningPageModell(SettingureEditDictator s) : base(s)
    {
        Sanitizing();
        Enable = s.Draft.Captioning.Enable;
        moreHere = string.Join("\r\n", s.Draft.Captioning.MoreHere);
        noMoreHere = string.Join("\r\n", s.Draft.Captioning.NoMoreHere);
        Sanitized();
    }

    [ObservableProperty]
    public partial bool Enable { get; set; }
 
    partial void OnEnableChanged(bool value) {
        session.Draft.Captioning.Enable = value;
        MarkDirty();
    }

    [Range(0, int.MaxValue)]
    public int HttpTimeout
    {
        get => session.Draft.Captioning.HttpTimeout;
        set
        {
            session.Draft.Captioning.HttpTimeout = value;
            MarkDirty();
            ValidateProperty(value);
        }
    }

    [Range(0, int.MaxValue)]
    public int AbortThreshold
    {
        get => session.Draft.Captioning.AbortThreshold;
        set
        {
            session.Draft.Captioning.AbortThreshold = value;
            MarkDirty();
            ValidateProperty(value);
        }
    }

    public IEnumerable<CaptionizerType> CaptionizerTypes => Enum.GetValues<CaptionizerType>();

    public CaptionizerType Type {
        get => session.Draft.Captioning.Type;
        set {
            session.Draft.Captioning.Type = value;
            MarkDirty();
        }
    }

    string moreHere;

    [RegexArray(MurArraySeparator.NewLine)]
    public string MoreHere
    {
        get => moreHere;
        set
        {
            moreHere = value;
            session.Draft.Captioning.MoreHere = [.. value.SplitNl()];
            MarkDirty();
            ValidateProperty(value);
        }
    }


    string noMoreHere;

    [RegexArray(MurArraySeparator.NewLine)]
    public string NoMoreHere
    {
        get => noMoreHere;
        set
        {
            noMoreHere = value;
            session.Draft.Captioning.NoMoreHere = [.. value.SplitNl()];
            MarkDirty();
            ValidateProperty(value);
        }
    }
}
