using CommunityToolkit.Mvvm.ComponentModel;
using ImogeIndex.Captionizers;
using ImogeIndexWPF.Settingure.Dictators;
using System.ComponentModel.DataAnnotations;

namespace ImogeIndexWPF.Settingure.Modelle.Pages.Captionizers;

/// <summary>
/// OpenAI互換設定
/// </summary>
internal partial class OpenAICompatPageModell: PageModell
{
    public OpenAICompatPageModell(SettingureEditDictator s) :base(s)
    {
        Sanitizing();
        UseCustomPrompt = s.Draft.Captioning.OpenAICompat.UseCustomPrompt;
        if (string.IsNullOrEmpty(CustomPrompt)) {
            CustomPrompt = OpenAICompatCaptionizer.SystemPrompt;
        }
        Sanitized();
    }

    [LooseUri]
    public string? Host
    {
        get => session.Draft.Captioning.OpenAICompat.Host;
        set
        {
            session.Draft.Captioning.OpenAICompat.Host = value;
            MarkDirty();
            ValidateProperty(value);
        }
    }

    public string? ApiKey
    {
        get => session.Draft.Captioning.OpenAICompat.ApiKey;
        set {
            session.Draft.Captioning.OpenAICompat.ApiKey = value;
            MarkDirty();
        }
    }

    public string? Model
    {
        get => session.Draft.Captioning.OpenAICompat.Model;
        set {
            MarkDirty();
            session.Draft.Captioning.OpenAICompat.Model = value;
        }
    }

    [Range(8, 15360)]
    public int ImageSize
    {
        get => session.Draft.Captioning.OpenAICompat.ImageSize;
        set
        {
            session.Draft.Captioning.OpenAICompat.ImageSize = value;
            MarkDirty();
            ValidateProperty(value);
        }
    }

    [Range(0, 8901)]
    public int Retry
    {
        get => session.Draft.Captioning.OpenAICompat.Retry;
        set
        {
            session.Draft.Captioning.OpenAICompat.Retry = value;
            MarkDirty();
            ValidateProperty(value);
        }
    }

    [Range(0.0f, float.MaxValue)]
    public float Temperature
    {
        get => session.Draft.Captioning.OpenAICompat.Temperature;
        set
        {
            session.Draft.Captioning.OpenAICompat.Temperature = value;
            MarkDirty();
            ValidateProperty(value);
        }
    }

    [Range(0.0f, float.MaxValue)]
    public float TopP
    {
        get => session.Draft.Captioning.OpenAICompat.TopP;
        set
        {
            session.Draft.Captioning.OpenAICompat.TopP = value;
            MarkDirty();
            ValidateProperty(value);
        }
    }

    [Range(0, int.MaxValue)]
    public int TopK
    {
        get => session.Draft.Captioning.OpenAICompat.TopK;
        set
        {
            session.Draft.Captioning.OpenAICompat.TopK = value;
            MarkDirty();
            ValidateProperty(value);
        }
    }

    [Range(0.0f, float.MaxValue)]
    public float RepeatPenalty
    {   
        get => session.Draft.Captioning.OpenAICompat.RepeatPenalty;
        set
        {
            session.Draft.Captioning.OpenAICompat.RepeatPenalty = value;
            MarkDirty();
            ValidateProperty(value);
        }
    }

    [Range(0, int.MaxValue)]
    public int MaxTokens
    {
        get => session.Draft.Captioning.OpenAICompat.MaxTokens;
        set
        {
            session.Draft.Captioning.OpenAICompat.MaxTokens = value;
            MarkDirty();
            ValidateProperty(value);
        }
    }


    [ObservableProperty]
    public partial bool UseCustomPrompt { get; set; }
    partial void OnUseCustomPromptChanged(bool value)
    {
        session.Draft.Captioning.OpenAICompat.UseCustomPrompt = value;
        MarkDirty();
    }

    public string CustomPrompt
    {
        get => session.Draft.Captioning.OpenAICompat.CustomPrompt;
        set
        {
            session.Draft.Captioning.OpenAICompat.CustomPrompt = value;
            MarkDirty();
            ValidateProperty(value);
        }
    }
}
