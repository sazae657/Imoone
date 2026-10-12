using ImogeIndex.Captionizers;
using ImogeIndex.Common;
using ImogeIndex.Storage;
using System.Diagnostics;

namespace ImogeIndex.Zenerators;


/// <summary>
/// ｷｬﾌﾟｼｮﾆﾝｸﾞ
/// </summary>
public class CaptionProzessor  : IDisposable, IProzessor {
    public event MessageDelegaty? OnMessage;
    public event ProgressDelegaty? OnProgress;
    public event StateDelegaty? OnStart;
    public event StateDelegaty? OnComplete;
    public event ErrorDelegaty? OnError;

    public event StateDelegaty? BeginVeryLongProcess;
    public event StateDelegaty? EndVeryLongProcess;

    List<FileRecord> filesIndex = new();
    private bool disposedValue;
    readonly ExecutionStateObzekt execution = new();
    public bool Running => execution.Running;

    Collective collective = new();

    Dictionary<long, ErrorRecord> mappedErrors = new();
    public bool SkipKnownError { get; set; } = false;

    /// <summary>
    /// ｷｬﾌﾟｼｮﾆﾝｸﾞ実行
    /// </summary>
    /// <param name="files"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    public async Task<IndexingResult> Run(IEnumerable<FileRecord> files, CancellationToken token) {
        var ret = new IndexingResult { };
        using var run = execution.TryStart();
        if (run == null) {
            return ret;
        }
        filesIndex.Clear();
        filesIndex.AddRange(files);
        mappedErrors.Clear();
        collective = new();

        await Task.Run(async () => {
            OnStart?.Invoke();
            Debug.WriteLine($"=== CaptionGenerator Start ===");
            try {
                if (token.IsCancellationRequested) {
                    return;
                }
                foreach (var x in IndexStore.Instance.ErrorAll()) {
                    if (token.IsCancellationRequested) {
                        return;
                    }
                    mappedErrors.Add(x.Id, x);
                }
                await ProcessCaption(token);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) {
                // 停止要求による中断は失敗にしない
            }
            catch(Exception r) {
                OnError?.Invoke(r, Properties.Resources.ZG_Error.Morfat(nameof(CaptionProzessor), r.Message));
                ret.Exception = r;
                ret.Success = false;
            }

        }, token).ContinueWith(r => {
            OnComplete?.Invoke();
        });
        ret.MarkCancelled(token);
        ret.Collective = collective;
        return ret;
    }

    async Task ProcessCaption(CancellationToken token) {
        if (!Profile.Active.CaptioningEnabled) {
            return;
        }
        if (Captionizer.Dictator == null) {
            return;
        }
        if (token.IsCancellationRequested) {
            return;
        }

        double c = 0;
        int errc = 0;
        Captionizer.Dictator.ResetFailures();
        foreach (var x in filesIndex) {
            if (token.IsCancellationRequested) {
                return;
            }
            c++;
            if (!x.IsAlive || x.HasCaption) {
                continue;
            }
            if (SkipKnownError && mappedErrors.ContainsKey(x.Id)){
                continue;
            }
            if (!IsTarget(x.Path)) {
                continue;
            }
            try {
                OnProgress?.Invoke((int)c, filesIndex.Count, errc, Properties.Resources.CP_Captioning, $"{x.Path}");
                var rpm = await Captionizer.Predict(x.Path, token);
                if (rpm == null) {
                    errc++;
                    continue;
                }
                collective.更();
                IndexStore.Instance.UpdateCaption(x, rpm, token);
                if (mappedErrors.ContainsKey(x.Id)) {
                    collective.更();
                    IndexStore.Instance.RemoveError(x.Id, token);
                }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) {
                return;
            }
            catch (CaptionAbortedException) {
                // ｻーﾊﾞーﾀﾞｳﾝ扱いで全体を打ち切る
                throw;
            }
            catch (CaptionFailureException cf) when (cf.Typo == CaptionErrorType.Fatal) {
                // 続けても無駄なので打ち切る
                throw;
            }
            catch (CaptionFailureException cf) {
                // この画像だけ失敗扱いにして続行
                // Unreachableはﾎｽﾄ復帰後に再処理したいのでｴﾗー記録しない
                if (cf.Typo == CaptionErrorType.Content) {
                    collective.更();
                    IndexStore.Instance.SetError(x.Id, ErrorType.Caption, token);
                }
                errc++;
                OnError?.Invoke(cf, Properties.Resources.ZG_Error.Morfat(nameof(CaptionProzessor),cf.Message));
            }
        }
    }

    /// <summary>
    /// それﾀーｹﾞｯﾄ？
    /// </summary>
    /// <param name="path"></param>
    /// <returns></returns>
    bool IsTarget(string path)
    {
        // 見ない方から先に評価しとく
        if (Profile.Active.CaptioningNoMoreHere.Count != 0) {
            if (Profile.Active.CaptioningNoMoreHere.Any(w => w.IsMatch(path))) {
                return false;
            }
        }

        if (Profile.Active.CaptioningMoreHere.Count == 0) {
            return true; // 空なら全部ﾀーｹﾞｯﾄ
        }
        return Profile.Active.CaptioningMoreHere.Any(w => w.IsMatch(path));
    }

    /// <summary>
    /// 完了待ち
    /// </summary>
    /// <returns></returns>
    public Task WaitFor() => execution.WaitFor();


    #region IDisposable
    protected virtual void Dispose(bool disposing) {
        if (!disposedValue) {
            disposedValue = true;
        }
    }

    public void Dispose() {
        // このｺーﾄﾞを変更しないでください。ｸﾘーﾝｱｯﾌﾟ ｺーﾄﾞを 'Dispose(bool disposing)' ﾒｿｯﾄﾞに記述します
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
    #endregion
}
