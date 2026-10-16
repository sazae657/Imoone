using ImogeIndex.Common;
using ImogeIndex.Web6;
using ImogeIndex.Mur;
using ImooneWPF.Properties;

namespace ImooneWPF;

/// <summary>
/// 表示文言(Resources.resxから引く)
/// </summary>
internal sealed class ResourcePalabras : IImoonePalabras
{
    public static ResourcePalabras Instance { get; } = new();

    public string ItemCount(int count) => Resources.MC_NItems.Morfat(count);

    public string SimilarSearchActive(string name, int limit) => Resources.MC_SimilarSearchActive.Morfat(name, limit);

    public string SimilarSearching => Resources.MC_SimilarSearching;

    public string SimilarSearchHint => Resources.MC_SimilarSearchHint;

    public string OpenSettingsFolder(string? profile)
        => profile == null ? Resources.MC_OpenSettingsFolder : Resources.MC_OpenSettingsFolder.Morfat(profile);

    public string LoadPhase(DatumLoadPhase phase) => phase switch {
        DatumLoadPhase.Allocating => Resources.MC_L_Allocating,
        DatumLoadPhase.Allocated => Resources.MC_L_Allocating,
        DatumLoadPhase.Loading => Resources.MC_L_Loading,
        DatumLoadPhase.Indexing => Resources.MC_L_Indexing,
        DatumLoadPhase.Complete => Resources.MC_L_Complete,
        _ => string.Empty,
    };

    public string DetailLabel(DetailFieldLabel label) => label switch {
        DetailFieldLabel.FileName => Resources.LI_FileName,
        DetailFieldLabel.CreatedAt => Resources.LH_CreatedAt,
        DetailFieldLabel.Location => Resources.LI_Location,
        DetailFieldLabel.FileSize => Resources.LI_FileSize,
        DetailFieldLabel.Resolution => Resources.LI_Resolution,
        _ => label.ToString(),
    };
}
