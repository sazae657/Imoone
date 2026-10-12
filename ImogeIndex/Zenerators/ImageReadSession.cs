using ImogeIndex.Common;
using ImogeIndex.Imoging;
using ImogeIndex.Sources;
using ImogeIndex.Storage;
using System.Collections.Concurrent;

namespace ImogeIndex.Zenerators;

/// <summary>
/// 失敗履歴付き画像読み込み
/// </summary>
public sealed class ImageReadSession {
    readonly string run = Guid.NewGuid().ToString("N");
    readonly ConcurrentDictionary<long, ImageReadRecord> records;
    readonly ConcurrentDictionary<long, ImageReadStatus> outcomes = new();
    readonly ConcurrentDictionary<long, ImageReadRecord> changes = new();
    readonly FileSourceRegistry sources;

    public ImageReadSession(IEnumerable<ImageReadRecord> history, FileSourceRegistry? sources = null) {
        records = new(history.ToDictionary(x => x.Id));
        this.sources = sources ?? FileSourceRegistry.Instance;
    }

    /// <summary>
    /// 再試行するべき？
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    public bool NeedRetry(long id) => records.TryGetValue(id, out var record) && !record.Succeeded;

    /// <summary>
    /// このﾚｺーﾄﾞもう消す？
    /// </summary>
    /// <param name="id"></param>
    /// <param name="threshold"></param>
    /// <returns></returns>
    public bool IsPurgeTarget(long id, int threshold) => threshold > 0 &&
        outcomes.TryGetValue(id, out var status) && status == ImageReadStatus.Corrupt &&
        records.TryGetValue(id, out var record) && record.FailureCount >= threshold;

    /// <summary>
    /// 読み込み
    /// </summary>
    /// <param name="file"></param>
    /// <param name="token"></param>
    /// <param name="minLongEdge">0より大きければ、長辺がこの値を下回らない範囲で縮小ﾃﾞｺーﾄﾞする</param>
    /// <returns></returns>
    public ImageTransaction? Load(FileRecord file, CancellationToken token, int minLongEdge = 0) {
        if (outcomes.TryGetValue(file.Id, out var previous) && previous != ImageReadStatus.Success) {
            return null;
        }
        var result = ImageReadResult.Read(sources, file.Path, token, minLongEdge);
        if (token.IsCancellationRequested) {
            result.Bitmap?.Dispose();
            token.ThrowIfCancellationRequested();
        }
        Observe(file.Id, result);
        if (result.Status != ImageReadStatus.Success) {
            Logger.Dictator.Error(
                result.Exception ?? new InvalidOperationException(Properties.Resources.CP_E_ImageLoadFailed.Morfat(file.Path)), $"{file.Path}: {result.Status}");
            return null;
        }
        return new ImageTransaction(result.Bitmap!);
    }

    /// <summary>
    /// 結果の観測
    /// </summary>
    /// <param name="id"></param>
    /// <param name="result"></param>
    public void Observe(long id, ImageReadResult result) {
        outcomes[id] = result.Status;
        var record = records.GetOrAdd(id, key => new ImageReadRecord { Id = key });
        lock (record) {
            record.Observe(result, run);
            changes[id] = record;
        }
    }

    /// <summary>
    /// 読み込み後の解析で失敗したのを記録
    /// 読めたときのﾊｯｼｭを引き継いで、壊れ判定なら失敗回数に数える
    /// </summary>
    /// <param name="id"></param>
    /// <param name="exception"></param>
    public void Fail(long id, Exception exception) {
        records.TryGetValue(id, out var record);
        Observe(id, new ImageReadResult(ImageReadResult.Court(exception),
            ContentHash: record?.ContentHash, Exception: exception));
    }

    /// <summary>
    /// I/O結果を保存
    /// </summary>
    /// <param name="token"></param>
    public void Flush(CancellationToken token) {
        if (changes.IsEmpty || token.IsCancellationRequested) {
            return;
        }
        IndexStore.Instance.SaveImageReadRecords(changes.Values, token);
        changes.Clear();
    }
}
