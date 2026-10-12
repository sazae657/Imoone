using CommunityToolkit.Mvvm.ComponentModel;
using ImogeIndex.Captionizers;
using ImogeIndexWPF.Settingure.Dictators;
using System.ComponentModel.DataAnnotations;

namespace ImogeIndexWPF.Settingure.Modelle.Pages.Captionizers;

/// <summary>
/// llama.cpp設定
/// </summary>
internal partial class LlamaCppPageModell : PageModell
{
    public LlamaCppPageModell(SettingureEditDictator s):base(s) {
        Sanitizing();
        UseCustomPrompt = s.Draft.Captioning.LLamaCpp.UseCustomPrompt;
        if (string.IsNullOrEmpty(CustomPrompt)) {
            CustomPrompt = LLamaCppCaptionizer.SystemPrompt;
        }
        Sanitized();
    }

    [LooseUri]
    public string? Host {
        get => session.Draft.Captioning.LLamaCpp.Host;
        set {
            session.Draft.Captioning.LLamaCpp.Host = value;
            MarkDirty();
            ValidateProperty(value);
        }
    }

    public string? ApiKey
    {
        get => session.Draft.Captioning.LLamaCpp.ApiKey;
        set {
            session.Draft.Captioning.LLamaCpp.ApiKey = value;
            MarkDirty();
        }
    }

    [Range(8, 15360)]
    public int ImageSize {
        get => session.Draft.Captioning.LLamaCpp.ImageSize;
        set {
            session.Draft.Captioning.LLamaCpp.ImageSize = value;
            MarkDirty();
            ValidateProperty(value);
        }
    }

    [Range(0, 8901)]
    public int Retry {
        get => session.Draft.Captioning.LLamaCpp.Retry;
        set {
            session.Draft.Captioning.LLamaCpp.Retry = value;
            MarkDirty();
            ValidateProperty(value);
        }
    }

    [Range(0.0f, float.MaxValue)]
    public float Temperature {
        get => session.Draft.Captioning.LLamaCpp.Temperature;
        set {
            session.Draft.Captioning.LLamaCpp.Temperature = value;
            MarkDirty();
            ValidateProperty(value);
        }
    }

    [Range(0.0f, float.MaxValue)]
    public float TopP {
        get => session.Draft.Captioning.LLamaCpp.TopP;
        set {
            session.Draft.Captioning.LLamaCpp.TopP = value;
            MarkDirty();
            ValidateProperty(value);
        }
    }

    [Range(0, int.MaxValue)]
    public int TopK {
        get => session.Draft.Captioning.LLamaCpp.TopK;
        set {
            session.Draft.Captioning.LLamaCpp.TopK = value;
            MarkDirty();
            ValidateProperty(value);
        }
    }

    [Range(0.0f, float.MaxValue)]
    public float RepeatPenalty {
        get => session.Draft.Captioning.LLamaCpp.RepeatPenalty;
        set {
            session.Draft.Captioning.LLamaCpp.RepeatPenalty = value;
            MarkDirty();
            ValidateProperty(value);
        }
    }

    [Range(0, int.MaxValue)]
    public int MaxTokens
    {
        get => session.Draft.Captioning.LLamaCpp.MaxTokens;
        set {
            session.Draft.Captioning.LLamaCpp.MaxTokens = value;
            MarkDirty();
            ValidateProperty(value);
        }
    }


    [ObservableProperty]
    public partial bool UseCustomPrompt { get; set; }
    partial void OnUseCustomPromptChanged(bool value)
    {
        session.Draft.Captioning.LLamaCpp.UseCustomPrompt = value;
        MarkDirty();
    }

    public string CustomPrompt
    {
        get => session.Draft.Captioning.LLamaCpp.CustomPrompt;
        set
        {
            session.Draft.Captioning.LLamaCpp.CustomPrompt = value;
            MarkDirty();
            ValidateProperty(value);
        }
    }

}
