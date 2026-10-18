using ImogeIndex.Mur;
using ImogeIndex.Storage;

namespace ImogeIndex.Web6.Storage;

public sealed record ProfileLoadResult(IReadOnlyList<FileItem> Items);

/// <summary>
/// ﾃﾞーﾀﾑ読み込みﾍﾙﾊﾟー
/// </summary>
/// <param name="texts">表示文言</param>
public sealed class ProfileLoader(IImoonePalabras texts)
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
                    LabelStore.Oppen();
                }
                catch {
                    // 一部だけ開いた状態を残さない
                    IndexStore.Close();
                    ThumbnailStore.Close();
                    LabelStore.Close();
                    throw;
                }
            }, cancellationToken);
            await datum.Load((p, value) => {
                phase(p, texts.LoadPhase(p));
                progress.Report(value);
            }, cancellationToken);
            return new ProfileLoadResult(datum.Items.ToArray());
        }
        finally
        {
            loadLock.Release();
        }
    }
}
