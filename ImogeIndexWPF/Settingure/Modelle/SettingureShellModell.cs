using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using ImogeIndexWPF.Settingure.Dictators;
using ImogeIndexWPF.Settingure.Modelle.Pages;
using ImogeIndexWPF.Settingure.Modelle.Pages.Captionizers;
using System.Collections.ObjectModel;

namespace ImogeIndexWPF.Settingure.Modelle;

/// <summary>
/// 設定画面ﾓﾃﾞｨﾙ
/// </summary>
internal sealed partial class SettingureShellModelle : ObservableObject
{
    private readonly ISettingsJail store;
    public SettingureEditDictator Session { get; private set; }
    public ObservableCollection<CategoryModelle> Categories { get; }

    [ObservableProperty]
    partial CategoryModelle? SelectedCategory { get; set; }

    [ObservableProperty]
    public partial PageModell? CurrentPage { get; set; }

    [ObservableProperty]
    public partial string? PageTitle { get; set; }

    [ObservableProperty]
    public partial string SettingureTitle { get; set; }

    [ObservableProperty]
    public partial int SelectedIndex { get; set; }


    public SettingureShellModelle(ISettingsJail jail, Type? initialPage = null)
    {
        store = jail;
        Session = new SettingureEditDictator(jail.Settings);
        Categories = CreateCategories(Session);
        if (initialPage != null) {
            SelectedIndex = Categories
                 .Select((x, i) => (x, i))
                 .FirstOrDefault(x => x.x.PageModelle.GetType() == initialPage)
                 .i;
        }
        else {
            SelectedIndex = 0;
        }
        SettingureTitle = $"{Properties.Resources.SGD_Settingure} - {jail.Settings.IPv7}";
    }



    [RelayCommand]
    private async Task Apply()
    {
        if (!CommitPendingEdit()) {
            return;
        }
        await TryCommitAsync();
    }

    [RelayCommand]
    private async Task Ok() {
        if (!CommitPendingEdit()) {
            return;
        }
        // 変更が無ければ保存せずに閉じる
        if (Session.IsDirty && !await TryCommitAsync()) {
            return;
        }
        WeakReferenceMessenger.Default.Send(new CloseSettingurewMessage());
    }

    /// <summary>
    /// 入力中の値を確定させる
    /// </summary>
    /// <returns>確定できたらtrue</returns>
    private static bool CommitPendingEdit()
    {
        var message = WeakReferenceMessenger.Default.Send(new CommitPendingEditMessage());
        return !message.HasReceivedResponse || message.Response;
    }

    /// <summary>
    /// 検証して保存する
    /// </summary>
    /// <returns>保存できたらtrue</returns>
    private async Task<bool> TryCommitAsync()
    {
        if (!ValidateAllPages(out var invalidCategory)) {
            if (invalidCategory is not null) {
                Navigate(invalidCategory);
            }
            return false;
        }

        try {
            // ｷｬﾝｾﾙ時は最後に保存した設定へ戻す
            Session.Original = await store.SaveAsync(Session.Draft);
        }
        catch (Exception ex) {
            store.ReportSaveFailure(ex);
            return false;
        }
        Session.IsDirty = false;
        return true;
    }

    [RelayCommand]
    private void Cancel()
    {
        Session.Reset();
        WeakReferenceMessenger.Default.Send(new CloseSettingurewMessage());
    }


    /// <summary>
    /// ﾍﾟーｼﾞ遷移
    /// </summary>
    /// <param name="category"></param>
    public void Navigate(CategoryModelle category)
    {
        if (!category.IsSelectable)
            return;

        SelectedCategory = category;
        CurrentPage = category.PageModelle;
        if (category.Title != category.Group) {
            PageTitle = $"{category.Group} > {category.Title}";
        }
        else {
            PageTitle = category.Title;
        }
    }

    /// <summary>
    /// 全ﾍﾟーｼﾞのValidateAllを呼ぶ
    /// </summary>
    /// <param name="invalidCategory"></param>
    /// <returns></returns>
    private bool ValidateAllPages(out CategoryModelle? invalidCategory)
    {
        foreach (var x in Categories) {
            if (x.PageModelle?.ValidateAll() ?? true) {
                continue;
            }
            invalidCategory = x;
            return false;
        }
        invalidCategory = null;
        return true;
    }

    /// <summary>
    /// ｶﾃｺﾞﾘーﾘｽﾄ組み立て
    /// </summary>
    /// <param name="session"></param>
    /// <returns></returns>
    ObservableCollection<CategoryModelle> CreateCategories(SettingureEditDictator session)
    {
        return [
            new CategoryModelle
            {
                Group = Properties.Resources.SG_General,
                Id = "General",
                Title = Properties.Resources.SG_General,
                PageModelle = new GeneralPageModell(session)
            },
            new CategoryModelle
            {
                Group = Properties.Resources.SG_General,
                Id = "Tagger",
                Title = Properties.Resources.SG_Tagger,
                PageModelle = new TaggerPageModell(session)
            },
            new CategoryModelle
            {
                Group = Properties.Resources.SG_Captionizer,
                Id = "Captioning",
                Title = Properties.Resources.SG_Captionizer,
                PageModelle = new CaptioningPageModell(session)
            },
            new CategoryModelle
            {
                Group = Properties.Resources.SG_Captionizer,
                Id = "Captionizers.Ollama",
                Title = Properties.Resources.SG_Cap_Ollama,
                PageModelle = new OllamaPageModell(session)
            },
            new CategoryModelle
            {
                Group = Properties.Resources.SG_Captionizer,
                Id = "Captionizers.LlamaCpp",
                Title = Properties.Resources.SG_Cap_LlamaCpp,
                PageModelle = new LlamaCppPageModell(session)
            },
            new CategoryModelle
            {
                Group = Properties.Resources.SG_Captionizer,
                Id = "Captionizers.OpenAICompat",
                Title = Properties.Resources.SG_Cap_OpenAICompat,
                PageModelle = new OpenAICompatPageModell(session)
            }
        ];
    }
}
