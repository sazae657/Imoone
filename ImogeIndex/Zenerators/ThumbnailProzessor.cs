using ImogeIndex.Common;
using ImogeIndex.Imoging;
using ImogeIndex.Storage;
using System.Diagnostics;

namespace ImogeIndex.Zenerators;

/// <summary>
/// ｻﾑﾈ処理
/// </summary>
public class ThumbnailProzessor : IProzessor, IDisposable
{
    readonly ExecutionStateObzekt execution = new();
    private bool disposedValue;
    public bool Running => execution.Running;

    public event StateDelegaty? BeginVeryLongProcess;
    public event StateDelegaty? EndVeryLongProcess;

    /// <summary>
    /// ｽｯﾄﾞﾚ数
    /// </summary>
    public int MaxThreads { get; set; } = 4;

    /// <summary>
    /// ｽｯﾄﾞﾚ毎の溜め込みﾊﾞｯﾌｧー数
    /// </summary>
    public int ImageBufferCount { get; set; } = 50;

    public event MessageDelegaty? OnMessage;
    public event ProgressDelegaty? OnProgress;
    public event SubProgressDelegaty? OnSubProgress;

    public event StateDelegaty? OnStart;
    public event StateDelegaty? OnComplete;
    public event ErrorDelegaty? OnError;

    Dictionary<string, FileRecord> filesIndex = new(StringComparer.OrdinalIgnoreCase);
    ImageReadSession imageReads = new([]);

    public async Task<IndexingResult> Run(CancellationToken token)
    {
        var ret = new IndexingResult { };
        using var run = execution.TryStart();
        if (run == null) {
            return ret;
        }
        await Task.Run(async() => {
            OnStart?.Invoke();
            try {
                FetchIndex(token);
                imageReads = new(IndexStore.Instance.GetImageReadRecords());
                await ProcessImages(token);
            }
            catch (Exception ex) when (ex.IsCancellationOf(token)) {
                // 停止要求による中断は失敗にしない
            }
            catch (Exception ex) {
                OnError?.Invoke(ex, Properties.Resources.ZG_Error.Morfat(nameof(ThumbnailProzessor), ex.Message));
                ret.Exception = ex;
                ret.Success = false;
            }
        }, token)
        .ContinueWith(t => {
            OnComplete?.Invoke();
        });
        ret.MarkCancelled(token);
        return ret;
    }

    void FetchIndex(CancellationToken token)
    {
        if (token.IsCancellationRequested) {
            return;
        }
        OnMessage?.Invoke(Properties.Resources.CP_LoadingIndex);
        filesIndex.Clear();
        foreach (var x in IndexStore.Instance.QueryAll(token)) {
            if (token.IsCancellationRequested) {
                return;
            }
            if (!filesIndex.TryAdd(x.Path, x)) {
                // 重複ﾚｺーﾄﾞの整理はIndexProzessorに任せる
                Logger.Dictator.Warn(Properties.Resources.TB_SkipDuplicateIndex.Morfat(x.Path));
            }
        }
        OnMessage?.Invoke(Properties.Resources.CP_IndexLoaded);
    }

    async Task ProcessImages(CancellationToken token)
    {
        int c = 0;
        int errc = 0;
        var trans = new List<FileRecord>();
        var div = Math.Max(1, MaxThreads * ImageBufferCount);
        var chunks = filesIndex
            .Select((v, i) => new { v, i })
            .GroupBy(x => x.i / div)
            .Select(g => g.Select(x => x.v));

        foreach (var chunk in chunks) {
            if (token.IsCancellationRequested) {
                return;
            }
            var chunkCount = chunk.Count();
            var subprog = chunkCount;
            var div2 = Math.Max(1, chunk.Count() / MaxThreads);
            var subchunks = chunk
                .Select((v, i) => new { v, i })
                .GroupBy(x => x.i / div2)
                .Select(g => g.Select(x => x.v));
            var pc = c / (double)filesIndex.Count;
            if (!token.IsCancellationRequested) {
                OnProgress?.Invoke(c, filesIndex.Count, errc, Properties.Resources.CP_Anyalize, chunk.First().Value.Path);
            }
            Dictionary<long, ErrorType> etra = new();
            ParallelOptions parallelOptions = new ParallelOptions {
                MaxDegreeOfParallelism = MaxThreads + 1,
                CancellationToken = token,
            };

            Parallel.ForEach(subchunks, parallelOptions, subchunk => {
                var btra = new List<FileRecord>();
                //using var hasher = new ImageHasher();
                foreach (var x in subchunk) {
                    if (token.IsCancellationRequested) {
                        return;
                    }
                    Interlocked.Increment(ref c);
                    Interlocked.Decrement(ref subprog);
                    if ((c % MaxThreads) == 0) {
                        OnProgress?.Invoke(c, filesIndex.Count, errc, Properties.Resources.CP_Anyalize, x.Value.Path);
                        OnSubProgress?.Invoke(0, subprog / (double)chunkCount);
                    }
                    Debug.WriteLine($"ProcessImages : {x.Value.Path}");
                    using var img = imageReads.Load(x.Value, token);
                    if (img == null) {
                        lock (etra) {
                            etra[x.Value.Id] = ErrorType.File;
                        }
                        Interlocked.Increment(ref errc);
                        continue;
                    }

                    // ｴﾝｺーﾄﾞで例外が出てもこのﾌｧｲﾙだけｴﾗー扱いにして続行する
                    byte[] thumbnail;
                    try {
                        thumbnail = ThumbnailEncoder.Encode(img.Bitmap, Profile.Active.Thumbnail.Size);
                    }
                    catch (OperationCanceledException) {
                        throw;
                    }
                    catch (Exception ex) {
                        imageReads.Fail(x.Value.Id, ex);
                        lock (etra) {
                            etra[x.Value.Id] = ErrorType.File;
                        }
                        Interlocked.Increment(ref errc);
                        Logger.Dictator.Error(ex, x.Value.Path);
                        OnError?.Invoke(ex, Properties.Resources.TB_E_CreateThumbnailFailed.Morfat(x.Value.Path, ex.Message));
                        continue;
                    }
                    x.Value.Thumbnail = thumbnail;
                    x.Value.HasThumbnail = true;
                    btra.Add(x.Value);
                }
                if (btra.Count == 0) {
                    return;
                }
                lock (trans) {
                    trans.AddRange(btra);
                }
            });
            OnSubProgress?.Invoke(-1, 0);
            if (trans.Count > 20) {
                OnProgress?.Invoke(c, filesIndex.Count, errc, Properties.Resources.CP_Saving, trans.Last().Path);
                ThumbnailStore.Instance.Set(trans, token);
                IndexStore.Instance.Update(trans, false, token);
                foreach (var x in trans) {
                    x.Thumbnail = null;
                }
                trans.Clear();
            }
            imageReads.Flush(token);
            if (etra.Count != 0) {
                foreach (var t in etra) {
                    IndexStore.Instance.SetError(t.Key, t.Value, token);
                }
            }
            if (token.IsCancellationRequested) {
                return;
            }
            OnProgress?.Invoke(c, filesIndex.Count, errc, Properties.Resources.CP_Anyalize, chunk.Last().Value.Path);
        }
        if (trans.Count != 0) {
            OnProgress?.Invoke(c, filesIndex.Count, errc, Properties.Resources.CP_Saving, trans.Last().Path);
            ThumbnailStore.Instance.Set(trans, token);
            IndexStore.Instance.Update(trans, false, token);
            foreach (var x in trans) {
                x.Thumbnail = null;
            }
            trans.Clear();
        }
        if (!token.IsCancellationRequested) {
            if (filesIndex.Count != 0) {
                OnProgress?.Invoke(c, filesIndex.Count, errc, Properties.Resources.CP_Anyalize, filesIndex.Last().Value.Path);
            }
        }
    }

    public Task WaitFor() => execution.WaitFor();

    #region IDisposable
    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue) {
            disposedValue = true;
        }
    }

    public void Dispose()
    {
        // このｺーﾄﾞを変更しないでください。ｸﾘーﾝｱｯﾌﾟ ｺーﾄﾞを 'Dispose(bool disposing)' ﾒｿｯﾄﾞに記述します
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
    #endregion
}
