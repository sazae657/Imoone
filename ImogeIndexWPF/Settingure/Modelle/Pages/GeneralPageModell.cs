using CommunityToolkit.Mvvm.ComponentModel;
using ImogeIndex;
using ImogeIndex.Common;
using ImogeIndexWPF.Settingure.Dictators;
using Microsoft.WindowsAppSDK.Runtime;
using System.ComponentModel.DataAnnotations;

namespace ImogeIndexWPF.Settingure.Modelle.Pages;


/// <summary>
/// 全般設定
/// </summary>
internal sealed partial class GeneralPageModell : PageModell
{
    public GeneralPageModell(SettingureEditDictator s) : base(s)
    {
        Sanitizing();
        searchPaths = string.Join("\r\n", s.Draft.SearchPaths);
        excludePaths = string.Join("\r\n", s.Draft.ExcludePaths);
        excludePatterns = string.Join("\r\n", s.Draft.ExcludePatterns);
        imageExtsnsions = string.Join("; ", s.Draft.ImageExtensions);
        metadataSearchTags = string.Join("\r\n",
            s.Draft.MetadataSearchTags.Count == 0 ?
            SystemParams.DefaultMetaAliases.Select(w => w.ToSettingure()) :
                s.Draft.MetadataSearchTags.Select(w => w.ToSettingure())
        );

        EnableThumbnail = s.Draft.Thumbnail.Enable;
        NiractionEnabled = s.Draft.Niraction.Enable;
        Sanitized();
    }

    string searchPaths;
    string excludePaths;
    string excludePatterns;
    string imageExtsnsions;
    string metadataSearchTags;

    [Required]
    public string SearchPaths
    {
        get => searchPaths;
        set {
            searchPaths = value;
            session.Draft.SearchPaths = [.. value.SplitNl()];
            MarkDirty();
            ValidateProperty(value);
        }
    }

    public string ExcludePaths
    {
        get => excludePaths;
        set
        {
            excludePaths = value;
            session.Draft.ExcludePaths = [.. value.SplitNl()];
            MarkDirty();
            ValidateProperty(value);
        }
    }

    [RegexArray(MurArraySeparator.NewLine)]
    public string ExcludePatterns {
        get => excludePatterns;
        set {
            excludePatterns = value;
            session.Draft.ExcludePatterns = [.. value.SplitNl()];
            MarkDirty();
            ValidateProperty(value);
        }
    }

    [RegexArray(MurArraySeparator.Semicolon)]
    public string ImageExtsnsions {
        get => imageExtsnsions;
        set {
            imageExtsnsions = value;
            session.Draft.ImageExtensions = [.. value.SplitSmc()];
            MarkDirty();
            ValidateProperty(value);
        }
    }

    [LooseMetaAlias]
    public string MetadataSearchTags {
        get => metadataSearchTags;
        set {
            metadataSearchTags = value;
            try {
                var aliases = value.SplitNl().Select(MetaAlias.FromSettingure).ToList();
                MetaAlias.ValidateType(aliases);
                session.Draft.MetadataSearchTags = aliases;
            }
            catch { }
            MarkDirty();
            ValidateProperty(value);
        }
    }

    [Range(1, 6)]
    public int LogLevel {
        get => session.Draft.LogLevel;
        set {
            session.Draft.LogLevel = value;
            MarkDirty();
            ValidateProperty(value);
        }
    }

    [Range(1, int.MaxValue)]
    public int Threads {
        get => session.Draft.Threads;
        set {
            session.Draft.Threads = value;
            MarkDirty();
            ValidateProperty(value);
        }
    }

    [Range(1, int.MaxValue)]
    public int BufferCount
    {
        get => session.Draft.BufferCount;
        set
        {
            session.Draft.BufferCount = value;
            MarkDirty();
            ValidateProperty(value);
        }
    }


    [Required]
    public string CacheDir {
        get => session.Draft.CacheDir;
        set {
            session.Draft.CacheDir = value;
            MarkDirty();
            ValidateProperty(value);
        }
    }

    [ObservableProperty]
    public partial bool EnableThumbnail { get; set; }
    partial void OnEnableThumbnailChanged(bool value) {
        session.Draft.Thumbnail.Enable = value;
        MarkDirty();
    }

    [Range(8, 15360)]
    public int ThumbnailSize {
        get => session.Draft.Thumbnail.Size;
        set {
            session.Draft.Thumbnail.Size = value;
            MarkDirty();
            ValidateProperty(value);
        }
    }

    [ObservableProperty]
    public partial bool NiractionEnabled { get; set; }
    partial void OnNiractionEnabledChanged(bool value)
    {
        session.Draft.Niraction.Enable = value;
        MarkDirty();
    }

    [LooseUri]
    public string? NiractionHost
    {
        get => session.Draft.Niraction.Host;
        set
        {
            session.Draft.Niraction.Host = value;
            MarkDirty();
            ValidateProperty(value);
        }
    }
}
