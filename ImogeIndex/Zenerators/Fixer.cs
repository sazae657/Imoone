using ImogeIndex.Common;
using ImogeIndex.Storage;

namespace ImogeIndex.Zenerators;

/// <summary>
/// ﾃﾞーﾀ補正
/// </summary>
internal sealed class Fixer
{
    public event ProgressDelegaty? OnProgress;

    /// <summary>
    /// FixDateTimeやる要件がなくなったので空実装で残しておく
    /// </summary>
    /// <param name="token"></param>
    /// <returns></returns>
    public Task Run(CancellationToken token)
    {
        //return Task.Run(() => {
        //    FixDateTime(token);
        //}, token);
        return Task.CompletedTask;
    }

    /// <summary>
    /// 日付の補正
    /// </summary>
    /// <param name="token"></param>
    void FixDateTime(CancellationToken token)
    {
        var lmc = IndexStore.Instance.GetImogeCount();
        var mtra = new Dictionary<long, DateTime>(lmc);
        foreach (var x in IndexStore.Instance.GetMetadataOfTag("Date/Time", token)) {
            if (token.IsCancellationRequested) {
                return;
            }
            if (x.Value == null) {
                continue;
            }
            if (x.Value.StartsWith("0000")) {
                continue;
            }
            if (!mtra.ContainsKey(x.ImageId)) {
                if (SloppyDateTime.TryParse(x.Value, out var dt)) {
                    mtra.Add(x.ImageId, dt);
                    continue;
                }
            }
        }
        var nql = $"UPDATE {IndexStore.Instance.TableOf<FileRecord>()} SET created_at=@created_at WHERE id=@id";
        IndexStore.Instance.TransactionOf(nql, (cmd, connection) => {
            cmd.Parameters.Add("@created_at", System.Data.DbType.Int64);
            cmd.Parameters.Add("@id", System.Data.DbType.Int64);
            foreach (var x in mtra) {
                if (token.IsCancellationRequested) {
                    return;
                }
                cmd.Parameters["@created_at"].Value = x.Value.AsUnixTimeSeconds();
                cmd.Parameters["@id"].Value = x.Key;
                cmd.ExecuteNonQuery();
            }
        }, token);

        mtra.Clear();
        OnProgress?.Invoke(0, lmc, 0, Properties.Resources.FX_TimestampSaved, "");
    }
}
