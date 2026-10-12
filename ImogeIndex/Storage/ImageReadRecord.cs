using ImogeIndex.Imoging;

namespace ImogeIndex.Storage;

/// <summary>
/// I/Oｴﾗー記録用
/// </summary>
[MurTable("image_read_failures")]
public sealed class ImageReadRecord {
    [MurColumn("id", MurColumnFlags.PrimaryKey)]
    public long Id { get; set; }
    [MurColumn("content_hash")]
    public string? ContentHash { get; set; }
    [MurColumn("failure_count")]
    public int FailureCount { get; set; }
    [MurColumn("last_run")]
    public string? LastRun { get; set; }
    public bool Succeeded { get; set; }

    /// <summary>
    /// 結果の観測
    /// </summary>
    /// <param name="result"></param>
    /// <param name="run"></param>
    public void Observe(ImageReadResult result, string run) {
        Succeeded = result.Status == ImageReadStatus.Success;
        if (result.ContentHash != null && ContentHash != result.ContentHash) {
            ContentHash = result.ContentHash;
            FailureCount = 0;
            LastRun = null;
        }
        if (result.Status == ImageReadStatus.Success) {
            ContentHash = result.ContentHash;
            FailureCount = 0;
            LastRun = null;
        }
        else if (result.Status == ImageReadStatus.Corrupt && result.ContentHash != null) {
            if (LastRun != run) {
                FailureCount = Math.Min(FailureCount, int.MaxValue - 1) + 1;
                LastRun = run;
            }
        }
    }
}
