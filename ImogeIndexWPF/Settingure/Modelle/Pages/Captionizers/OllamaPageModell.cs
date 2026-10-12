using CommunityToolkit.Mvvm.ComponentModel;
using ImogeIndex.Captionizers;
using ImogeIndexWPF.Settingure.Dictators;
using System.ComponentModel.DataAnnotations;

namespace ImogeIndexWPF.Settingure.Modelle.Pages.Captionizers;

/// <summary>
/// ollama設定
/// </summary>
internal partial class OllamaPageModell : PageModell {
    public OllamaPageModell(SettingureEditDictator s):base(s) {
        Sanitizing();
        UseCustomPrompt = s.Draft.Captioning.Ollama.UseCustomPrompt;
        if (string.IsNullOrEmpty(CustomPrompt)) {
            CustomPrompt = OllamaCaptionizer.SystemPrompt;
        }
        Sanitized();
    }

    [LooseUri]
    public string? Host {
        get => session.Draft.Captioning.Ollama.Host;
        set {
            session.Draft.Captioning.Ollama.Host = value;
            MarkDirty();
            ValidateProperty(value);
        }
    }

    public string? Model {
        get => session.Draft.Captioning.Ollama.Model;
        set {
            session.Draft.Captioning.Ollama.Model = value;
            MarkDirty();
        }
    }

    [Range(8, 15360)]
    public int ImageSize {
        get => session.Draft.Captioning.Ollama.ImageSize;
        set {
            session.Draft.Captioning.Ollama.ImageSize = value;
            MarkDirty();
            ValidateProperty(value);
        }
    }

    [Range(0, 8901)]
    public int Retry {
        get => session.Draft.Captioning.Ollama.Retry;
        set {
            session.Draft.Captioning.Ollama.Retry = value;
            MarkDirty();
            ValidateProperty(value);
        }
    }

    [Range(0.0f, float.MaxValue)]
    public float Temperature {
        get => session.Draft.Captioning.Ollama.Temperature;
        set {
            session.Draft.Captioning.Ollama.Temperature = value;
            MarkDirty();
            ValidateProperty(value);
        }
    }

    [Range(0.0f, float.MaxValue)]
    public float TopP {
        get => session.Draft.Captioning.Ollama.TopP;
        set {
            session.Draft.Captioning.Ollama.TopP = value;
            MarkDirty();
            ValidateProperty(value);
        }
    }

    [Range(0, int.MaxValue)]
    public int TopK {
        get => session.Draft.Captioning.Ollama.TopK;
        set {
            session.Draft.Captioning.Ollama.TopK = value;
            MarkDirty();
            ValidateProperty(value);
        }
    }

    [Range(0.0f, float.MaxValue)]
    public float RepeatPenalty {
        get => session.Draft.Captioning.Ollama.RepeatPenalty;
        set {
            session.Draft.Captioning.Ollama.RepeatPenalty = value;
            MarkDirty();
            ValidateProperty(value);
        }
    }

    [Range(0, int.MaxValue)]
    public int NumCtx {
        get => session.Draft.Captioning.Ollama.NumCtx;
        set {
            session.Draft.Captioning.Ollama.NumCtx = value;
            MarkDirty();
            ValidateProperty(value);
        }
    }

    [Range(0, int.MaxValue)]
    public int NumPredict {
        get => session.Draft.Captioning.Ollama.NumPredict;
        set {
            session.Draft.Captioning.Ollama.NumPredict = value;
            MarkDirty();
            ValidateProperty(value);
        }
    }

    [ObservableProperty]
    public partial bool UseCustomPrompt { get; set; }
    partial void OnUseCustomPromptChanged(bool value)
    {
        session.Draft.Captioning.Ollama.UseCustomPrompt = value;
        MarkDirty();
    }

    public string CustomPrompt
    {
        get => session.Draft.Captioning.Ollama.CustomPrompt;
        set
        {
            session.Draft.Captioning.Ollama.CustomPrompt = value;
            MarkDirty();
            ValidateProperty(value);
        }
    }

}
