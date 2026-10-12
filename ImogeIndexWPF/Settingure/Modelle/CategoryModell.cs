using ImogeIndexWPF.Settingure.Modelle.Pages;
using System.Collections.ObjectModel;

namespace ImogeIndexWPF.Settingure.Modelle;

/// <summary>
/// 設定ｶﾃｺﾞﾘー
/// </summary>
internal sealed class CategoryModelle
{
    public required string Id { get; init; }
    public required string Title { get; init; }

    public required string Group { get; init; }

    public ObservableCollection<CategoryModelle> Children
    {
        get;
    } = [];

    required public PageModell PageModelle { get; init; }

    public bool IsSelectable => PageModelle is not null;
}
