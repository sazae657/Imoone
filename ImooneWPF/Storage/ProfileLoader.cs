using ImogeIndex.Mur;
using ImogeIndex.Storage;

namespace ImooneWPF.Storage;

internal sealed record ProfileLoadResult(IReadOnlyList<FileItem> Items);

/// <summary>
/// ﾃﾞーﾀﾑ読み込みﾍﾙﾊﾟー
/// </summary>
internal sealed class ProfileLoader
{
    readonly SemaphoreSlim loadLock = new(1, 1);
    readonly ImogeDatum datum = new();

    public ImogeDatum Datum => datum;

    public async Task<ProfileLoadResult> LoadAsync(
        IProgress<double> progress,
        Action<DatumLoadPhase, string> phase,
        CancellationToken cancellationToken)
    {
        await loadLock.WaitAsync(cancellationToken);
        try
        {
            await Task.Run(() =>
            {
                try {
                    IndexStore.Oppen(false);
                    ThumbnailStore.Oppen(false);
                }
                catch {
                    // 片方だけ開いた状態を残さない
                    IndexStore.Close();
                    ThumbnailStore.Close();
                    throw;
                }
            }, cancellationToken);
            await datum.Load((p, value) => {
                phase(p, PhaseText(p));
                progress.Report(value);
            }, cancellationToken);
            return new ProfileLoadResult(datum.Items.ToArray());
        }
        finally
        {
            loadLock.Release();
        }
    }

    /// <summary>
    /// 読み込み段階の表示文言
    /// </summary>
    /// <param name="phase"></param>
    /// <returns></returns>
    private static string PhaseText(DatumLoadPhase phase) => phase switch {
        DatumLoadPhase.Allocating => Properties.Resources.MC_L_Allocating,
        DatumLoadPhase.Allocated => Properties.Resources.MC_L_Allocating,
        DatumLoadPhase.Loading => Properties.Resources.MC_L_Loading,
        DatumLoadPhase.Indexing => Properties.Resources.MC_L_Indexing,
        DatumLoadPhase.Complete => Properties.Resources.MC_L_Complete,
        _ => string.Empty,
    };
}
