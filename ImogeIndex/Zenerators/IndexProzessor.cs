using ImogeIndex.Common;
using ImogeIndex.Imoging;
using ImogeIndex.Sources;
using ImogeIndex.Storage;
using ImogeIndex.Taggers;
using Microsoft.ML.OnnxRuntime;
using SkiaSharp;
using System.Diagnostics;


namespace ImogeIndex.Zenerators;


/// <summary>
/// ｲﾝﾃﾞｯｸｽ生成結果
/// </summary>
public sealed class IndexingResult : IIndexingResult {
    public bool Success { get; set; } = true;
    public Exception? Exception { get; set; }
    public bool Cancelled { get; set; }

    public Collective Collective { get; set; } = new();

    public int Found { get; set; }
    public int SkippedDirectories { get; set; }

    /// <summary>
    /// 読めなかったので次回に回したﾌｧｲﾙ数
    /// </summary>
    public int FailedFiles { get; set; }

    public bool WasFailed() {
        return !Success && !Cancelled;
    }

    /// <summary>
    /// 停止要求が来ていて失敗していなければ中断扱いにする
    /// </summary>
    /// <param name="token"></param>
    public void MarkCancelled(CancellationToken token) {
        if (Success && token.IsCancellationRequested) {
            Cancelled = true;
        }
    }
}

/// <summary>
/// 画像蓄えﾏﾝ
/// </summary>
public class ImageTransaction : IDisposable
{
    SKBitmap? bitmap;
    public SKBitmap Bitmap => bitmap!;

    public ulong PHash { get; set; }
    public ulong AHash { get; set; }
    public ulong DHash { get; set; }
    public ulong WHash { get; set; }

    public FileRecord? File { get; set; }

    public ImageTransaction(SKBitmap bitmap) { this.bitmap = bitmap; }

    public void Dispose()
    {
        bitmap?.Dispose();
        bitmap = null;
    }
}

/// <summary>
/// ｲﾝﾃﾞｯｸｽ生成
/// </summary>
public class IndexProzessor : IDisposable, IProzessor {
    private bool disposedValue;
    readonly ExecutionStateObzekt execution = new();
    public bool Running => execution.Running;

    public event StateDelegaty? BeginVeryLongProcess;
    public event StateDelegaty? EndVeryLongProcess;

    public event MessageDelegaty? OnMessage;
    public event ProgressDelegaty? OnProgress;
    public event SubProgressDelegaty? OnSubProgress;

    public event StateDelegaty? OnStart;
    public event StateDelegaty? OnComplete;
    public event ErrorDelegaty? OnError;

    /// <summary>
    /// ｽｯﾄﾞﾚ数
    /// </summary>
    public int MaxThreads { get; set; } = 4;

    /// <summary>
    /// ｽｯﾄﾞﾚ毎の溜め込みﾊﾞｯﾌｧー数
    /// </summary>
    public int ImageBufferCount { get; set; } = 50;

    /// <summary>
    /// ﾒﾀﾃﾞーﾀﾑ再取得
    /// </summary>
    public bool RescanMetadata { get; set; } = false;

    /// <summary>
    /// ﾀｲﾑｽﾀﾝﾌﾟが変わっていないﾌｫﾙﾀﾞの列挙を省略する(NTFSの仕様上、中身が差し替わっただけの更新を取れない)
    /// </summary>
    public bool QuickScan { get; set; } = false;

    /// <summary>
    /// 更新されたﾌｧｲﾙのｷｬﾌﾟｼｮﾝを破棄して作り直させる
    /// </summary>
    public bool RecaptionUpdated { get; set; } = true;


    /// <summary>
    /// 何ﾚｺーﾄﾞ溜めたらｺﾐｯﾄするか
    /// </summary>
    public int CommitFreq { get; set; } = 20;

    // 場所の比較は供給元に従う(ﾛーｶﾙは設定の表記揺れで別物扱いしないようにIgnoreCase)
    Dictionary<string, FileRecord> filesIndex;

    /// <summary>
    /// ﾌｧｲﾙ
    /// </summary>
    public IEnumerable<FileRecord> Files => filesIndex.Values;

    Dictionary<string, DirectoryIndexRecord> directoryIndex;

    // 何らかの事情で重複していた既存ﾚｺーﾄﾞ(完走したら消す用)
    readonly List<FileRecord> duplicateFiles = new();
    // 重複ﾌｫﾙﾀﾞー
    readonly List<DirectoryIndexRecord> duplicateDirectories = new();

    Collective collective = new();

    ImageReadSession imageReads;
    readonly FileSourceRegistry sources;
    DirectoryScanState scanState;
    readonly HashSet<FileRecord> deferredFiles = new();

    // QuickScanでﾌｫﾙﾀﾞー単位にﾚｺーﾄﾞを引く用
    ILookup<string, FileRecord>? filesByContainer;

    public IndexProzessor() : this(FileSourceRegistry.Instance) { }

    internal IndexProzessor(FileSourceRegistry sources) {
        this.sources = sources;
        filesIndex = new(sources.Comparer);
        directoryIndex = new(sources.Comparer);
        imageReads = new([], sources);
        scanState = new(sources);
    }

    /// <summary>
    /// この回数I/Oｴﾗーを繰り返したらﾚｺーﾄﾞを削除する(0以下は消さない)
    /// </summary>
    public int CorruptFailureThreshold { get; set; } = 3;

    int indexed = 0;
    int found = 0;
    int updated = 0;

    /// <summary>
    /// 更新候補(実ﾌｧｲﾙとｲﾝﾃﾞｯｸｽの組)
    /// </summary>
    record Suspect(SourceFile Info, FileRecord Record);

    // 更新を検出したﾚｺーﾄﾞ
    List<FileRecord> updatedFiles = new();

    public async Task<IndexingResult> Run(CancellationToken token) {
        var ret = new IndexingResult { };
        using var run = execution.TryStart();
        if (run == null) {
            return ret;
        }
        collective = new();
        scanState = new(sources);
        deferredFiles.Clear();
        updatedFiles.Clear();
        updated = 0;
        await Task.Run(async() => {
            OnStart?.Invoke();
            Debug.WriteLine($"=== IndexGenerator Start ===");
            try {
                var frc = await FetchIndex(false, token);
                if (frc == FetchIndexResult.NeedRescan) {
                    // ﾘｽｷｬﾝ
                    await FetchIndex(true, token);
                }
                // ﾌｫﾙﾀﾞーごとにfilesIndexを全件走査すると件数xﾌｫﾙﾀﾞー数になるので先に引けるようにしておく
                filesByContainer = QuickScan
                    ? filesIndex.Values.ToLookup(w => w.Container, sources.Comparer)
                    : null;
                foreach (var x in Profile.Active.SearchPaths) {
                    if (token.IsCancellationRequested) {
                        break;
                    }
                    // 未登録のｽｷーﾑなどはそのﾙーﾄだけ後回しにする
                    if (!TryReadDirectory(x, () => {
                        var source = sources.Resolve(x);
                        return (Source: source, Location: source.Normalize(x));
                    }, out var root)) {
                        continue;
                    }
                    await FindFiles(root.Source, root.Location, token);
                }
                filesByContainer = null;
                if (token.IsCancellationRequested) {
                    return;
                }
                PreserveDeferred();
                // 停止要求による中断は外側のIsCancellationOfで処理する
                Refilter(token);
                if (token.IsCancellationRequested) {
                    return;
                }
                InvalidateUpdated(token);
                IndexStore.Instance.SaveDirectoryIndex(directoryIndex.Values, token);
                try {
                    await ProcessImages(token);
                    await ProcessTagging(token);
                }
                catch (Exception ex) when (ex.IsCancellationOf(token)) {
                    // 停止要求による中断は失敗にしない
                    return;
                }
                imageReads.Flush(token);
                PurgeMissing(token);
                if (scanState.Count != 0 || scanState.FailedFiles != 0) {
                    OnMessage?.Invoke(Properties.Resources.CP_IndexingCompleted.Morfat(scanState.Count, scanState.FailedFiles));
                }
            }
            catch (Exception ex) when (ex.IsCancellationOf(token)) {
                // 停止要求による中断は失敗にしない
            }
            catch (Exception ex) {
                OnError?.Invoke(ex, Properties.Resources.ZG_Error.Morfat(nameof(IndexProzessor), ex.Message));
                ret.Exception = ex;
                ret.Success = false;
            }

        }, token)
        .ContinueWith(t => {
            OnComplete?.Invoke();
        });
        ret.MarkCancelled(token);
        ret.Collective = collective;
        ret.Found = found;
        ret.SkippedDirectories = scanState.Count;
        ret.FailedFiles = scanState.FailedFiles;
        return ret;
    }

    public Task WaitFor() => execution.WaitFor();

    /// <summary>
    /// ｲﾝﾃﾞｯｸｽ読んだあとどうする？
    /// </summary>
    enum FetchIndexResult
    {
        OK,
        Aborted,
        NeedRescan
    };

    /// <summary>
    /// ｲﾝﾃﾞｯｸｽ読み込み＆ﾁｪｯｸ
    /// </summary>
    /// <param name="fetchOnly"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    async Task<FetchIndexResult> FetchIndex(bool fetchOnly, CancellationToken token) {
        if (token.IsCancellationRequested) {
            return FetchIndexResult.OK;
        }
        OnMessage?.Invoke(Properties.Resources.CP_LoadingIndex);
        var cn = IndexStore.Instance.GetImogeCount();
        filesIndex = new Dictionary<string, FileRecord>(cn, sources.Comparer);
        duplicateFiles.Clear();
        duplicateDirectories.Clear();
        foreach (var x in IndexStore.Instance.QueryAll(token)) {
            if (token.IsCancellationRequested) {
                return FetchIndexResult.Aborted;
            }
            if (x == null) {
                continue;
            }
            if (filesIndex.TryGetValue(x.Path, out var existing)) {
                // 解析成果を持ってる方を残す
                var keep = Score(x) > Score(existing) ? x : existing;
                var drop = ReferenceEquals(keep, x) ? existing : x;
                Logger.Dictator.Warn($"大文字小文字違いの重複レコード: {drop.Path} => {keep.Path}");
                filesIndex[x.Path] = keep;
                duplicateFiles.Add(drop);
                continue;
            }
            filesIndex.Add(x.Path, x);
            if ((filesIndex.Count % 1000) == 0) {
                OnMessage?.Invoke(Properties.Resources.CP_LoadIndexProgress.Morfat(filesIndex.Count, cn));
                OnSubProgress?.Invoke(0, filesIndex.Count/ (double)cn);
            }
        }
        OnMessage?.Invoke(Properties.Resources.CP_LoadingIndex);
        OnSubProgress?.Invoke(0, 0);
        indexed = filesIndex.Count;
        found = 0;

        directoryIndex.Clear();
        if (token.IsCancellationRequested) {
            return FetchIndexResult.Aborted;
        }
        foreach (var x in IndexStore.Instance.GetDirectoryIndex()) {
            if (token.IsCancellationRequested) {
                return FetchIndexResult.Aborted;
            }
            if (!directoryIndex.TryAdd(x.Path, x)) {
                duplicateDirectories.Add(x);
            }
        }
        imageReads = new(IndexStore.Instance.GetImageReadRecords(), sources);
        bool rescan = false;
        if (!fetchOnly) {
            if (RescanMetadata) {
                rescan = false; // Fixer原状空実装なので
                OnMessage?.Invoke(Properties.Resources.CP_CheckingTimestamp);
                var fix = new Fixer();
                fix.OnProgress += (fc, ftotal, ferror, fphase, fmessage) => OnProgress?.Invoke(fc, ftotal, ferror, fphase, fmessage);
                await fix.Run(token);
            }
            if (Profile.Active.Thumbnail.Enable) {
                OnMessage?.Invoke(Properties.Resources.CP_CheckingThumbnail);
                BeginVeryLongProcess?.Invoke();
                try {
                    await ThumbnailStore.Instance.CheckThumbnail(filesIndex.Values, token);
                }
                finally {
                    EndVeryLongProcess?.Invoke();
                }
            }
        }
        OnMessage?.Invoke(Properties.Resources.CP_IndexLoaded);

        return rescan ? FetchIndexResult.NeedRescan : FetchIndexResult.OK;

        static int Score(FileRecord r) => (r.HasCaption ? 2 : 0) + (r.HasTag ? 1 : 0);
    }

    /// <summary>
    /// ﾌｨﾙﾀー再適用
    /// </summary>
    /// <param name="token"></param>
    void Refilter(CancellationToken token)
    {
        // TODO: 設定差分見て省略したい
        var excludes = filesIndex.Values.AsParallel()
                    .WithCancellation(token)
                    .Where(w =>
            !Profile.Active.WasTargetImage(w.Path) ||
            !Profile.Active.WasIncludePath(w.Container) ||
            Profile.Active.WasExcludePath(w.Container));
        foreach (var x in excludes) {
            if (x.HasCaption && Exists(x.Path)) {
                // ｷｬﾌﾟｼｮﾝは作り直すのが高いので、実在すれば残す
                // 探索対象外なのでIsAliveを明示し、解析などの処理はしないよう保護する
                x.IsAlive = true;
                deferredFiles.Add(x);
                continue;
            }
            x.IsAlive = false;
        }

        var missingDir = directoryIndex.Values.Where(w => !Profile.Active.WasIncludePath(w.Path) || Profile.Active.WasExcludePath(w.Path));
        foreach (var x in missingDir) {
            x.IsAlive = false;
        }
    }

    /// <summary>
    /// 見つからんｴﾝﾄﾘーを削除
    /// </summary>
    /// <param name="token"></param>
    void PurgeMissing(CancellationToken token) {
        if (token.IsCancellationRequested) {
            return;
        }
        var missings = filesIndex.Values.Where(w => !w.IsAlive ||
            (!deferredFiles.Contains(w) && imageReads.IsPurgeTarget(w.Id, CorruptFailureThreshold))).ToList();
        collective.削(IndexStore.Instance.Purge(missings, token));
        foreach (var x in missings) {
            filesIndex.Remove(x.Path);
        }

        if (duplicateFiles.Count != 0) {
            collective.削(IndexStore.Instance.Purge(duplicateFiles, token));
            duplicateFiles.Clear();
        }

        var missingDir = directoryIndex.Values.Where(w => !w.IsAlive).ToList();
        IndexStore.Instance.Purge(missingDir, token);
        if (duplicateDirectories.Count != 0) {
            IndexStore.Instance.Purge(duplicateDirectories, token);
            duplicateDirectories.Clear();
        }
        foreach (var x in missingDir) {
            collective.削();
            directoryIndex.Remove(x.Path);
        }
    }


    /// <summary>
    /// 未探索のﾚｺーﾄﾞを保持して次回の再走査を予約
    /// </summary>
    void PreserveDeferred() {
        if (scanState.Count == 0) {
            return;
        }
        foreach (var file in filesIndex.Values) {
            if (scanState.Contains(file.Container)) {
                file.IsAlive = true;
                deferredFiles.Add(file);
            }
        }
        foreach (var directory in directoryIndex.Values) {
            if (scanState.Contains(directory.Path)) {
                directory.IsAlive = true;
                directory.LastModified = DirectoryScanState.NeedsRescan;
            }
        }
    }


    /// <summary>
    /// 実在する？(探索対象外のﾚｺーﾄﾞ保護用)
    /// </summary>
    /// <param name="location"></param>
    /// <returns></returns>
    bool Exists(string location) {
        try {
            return sources.TryResolve(location, out var source) && source.TryGetFile(location) != null;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException) {
            // File.Existsと同じく、確かめられないものは無い扱い
            return false;
        }
    }

    /// <summary>
    /// I/Oｴﾗー起こしたﾔﾂをﾌｫﾙﾀﾞー単位で隔離する
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="location"></param>
    /// <param name="read"></param>
    /// <param name="result"></param>
    /// <returns></returns>
    bool TryReadDirectory<T>(string location, Func<T> read, out T result) {
        try {
            result = read();
            return true;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException) {
            Logger.Dictator.Error(ex, location);
            DeferDirectory(location, ex.Message);
            result = default!;
            return false;
        }
    }

    /// <summary>
    /// ｽｷｯﾌﾟ扱いに
    /// </summary>
    /// <param name="location"></param>
    /// <param name="reason"></param>
    void DeferDirectory(string location, string reason) {
        if (scanState.Defer(location)) {
            var message = Properties.Resources.CP_SkipDirectory.Morfat(location, reason);
            Logger.Dictator.Warn(message);
            OnMessage?.Invoke(message);
        }
    }

    /// <summary>
    /// 一部のﾌｧｲﾙが読めなかったので、そのﾌｫﾙﾀﾞーだけ次回再列挙させる
    /// </summary>
    /// <param name="location"></param>
    /// <param name="failedFiles"></param>
    void RetryDirectory(string location, int failedFiles) {
        scanState.Retry(location, failedFiles);
        var message = Properties.Resources.CP_DeferredFile.Morfat(location, failedFiles);
        Logger.Dictator.Warn(message);
        OnMessage?.Invoke(message);
    }

    /// <summary>
    /// 日付ﾁｪｯｸ付きﾌｧｲﾙ列挙
    /// </summary>
    /// <param name="source"></param>
    /// <param name="location">正規化済みの場所</param>
    /// <param name="token"></param>
    /// <returns></returns>
    async Task FindFiles(IFileSource source, string location, CancellationToken token) {
        if (token.IsCancellationRequested) {
            return;
        }
        if (Profile.Active.WasExcludePath(location)) {
            return;
        }
        OnMessage?.Invoke($"Search: {indexed} ({found}) {location}");
        // 列挙時の情報は古いかもしれないので、入る直前に取り直す
        if (!TryReadDirectory(location, () => source.GetDirectory(location), out var dir)) {
            return;
        }
        if (dir.IsLink) {
            // TODO: 再解析ﾎﾟｲﾝﾄの扱い面倒臭いので原状はｽｷｯﾌﾟ
            DeferDirectory(location, Properties.Resources.CP_SkipReparsePoint);
            return;
        }
        if (!directoryIndex.TryGetValue(location, out var di)) {
            di = new DirectoryIndexRecord { Path = location };
            directoryIndex.Add(location, di);
        }
        di.IsAlive = true;
        // 日付があてにならない供給元(null)は毎回列挙する
        var timestampRead = TryReadDirectory(location, () => source.GetTimestamp(dir), out var dts);
        bool modified = !timestampRead || dts == null || dts != di.LastModified;
        di.LastModified = DirectoryScanState.NeedsRescan;

        var directoriesRead = TryReadDirectory(location, () => source.GetDirectories(dir), out var directories);
        if (directoriesRead) {
            foreach (var d in directories) {
                if (token.IsCancellationRequested) {
                    return;
                }
                await FindFiles(source, d.Location, token);
            }
        }
        if (token.IsCancellationRequested) {
            return;
        }

        //TODO: NTFSはﾌｧｲﾙの中身を上書きしてもﾌｫﾙﾀﾞのLastWriteTimeが変わらない
        //TODO: ﾌｫﾙﾀﾞの日付だけで列挙を省略すると更新を取りこぼす
        //TODO: つう事で省略はQuickScan指定時のみ
        //TODO: これ何とかならねえのかな
        if (filesByContainer != null && !modified && directoriesRead) {
            foreach (var x in filesByContainer[location]) {
                if (token.IsCancellationRequested) {
                    return;
                }
                x.IsAlive = true;
            }
            di.LastModified = dts!.Value;
            return;
        }

        var files = new List<SourceFile>();
        var suspects = new List<Suspect>();
        if (!TryReadDirectory(location, () => source.GetFiles(dir), out var entries)) {
            return;
        }
        // ﾌｧｲﾙ単位の失敗はﾌｫﾙﾀﾞーごと後回しにせず、そのﾌｧｲﾙだけ次回に回す
        int failedFiles = 0;
        foreach (var x in entries.Where(Profile.Active.WasTargetImage)) {
            if (token.IsCancellationRequested) {
                return;
            }
            if (filesIndex.TryGetValue(x.Location, out var sp)) {
                sp.IsAlive = true;
                // 一次判定(日付とｻｲｽﾞは列挙時に取得済みなのでI/Oは発生しない)
                if (sp.IsStampMismatch(x)) {
                    suspects.Add(new Suspect(x, sp));
                }
                continue;
            }
            files.Add(x);
        }
        if (suspects.Count != 0) {
            OnMessage?.Invoke(Properties.Resources.CP_InspectUpdate.Morfat(suspects.Count, location));
            failedFiles += InspectUpdated(source, suspects, token);
        }
        if (files.Count != 0) {
            var div = Math.Max(1, files.Count / MaxThreads);
            var chunks = files
                .Select((v, i) => new { v, i })
                .GroupBy(x => x.i / div)
                .Select(g => g.Select(x => x.v));
            List<FileTransaction> trans = new();
            int failCount = 0;
            Parallel.ForEach(chunks, c => {
                foreach (var x in c) {
                    if (token.IsCancellationRequested) {
                        return;
                    }

                    var it = FileTransaction.FromFile(source, x);
                    if (it == null){ 
                        Interlocked.Increment(ref failCount);
                        continue; 
                    }
                    lock (trans) {
                        trans.Add(it);
                    }
                }
            });
            failedFiles += failCount;
            if (token.IsCancellationRequested) {
                return;
            }
            foreach (var x in IndexStore.Instance.AddFiles(trans, token)) {
                x.IsAlive = true;
                filesIndex.Add(x.Path, x);
                found++;
                collective.追();
            }
        }
        if (failedFiles != 0) {
            RetryDirectory(location, failedFiles);
        }
        if (!token.IsCancellationRequested && timestampRead && dts != null && directoriesRead && !scanState.ShouldRescan(location)) {
            di.LastModified = dts.Value;
        }
    }

    /// <summary>
    /// 更新候補のﾊｯｼｭを取り直して更新を確定させる
    /// </summary>
    /// <param name="source"></param>
    /// <param name="suspects"></param>
    /// <param name="token"></param>
    /// <returns>読めなかった件数(読めなかったﾚｺーﾄﾞは次回まで保護する)</returns>
    int InspectUpdated(IFileSource source, List<Suspect> suspects, CancellationToken token) {
        var confirmed = new List<FileRecord>();
        var failed = new List<FileRecord>();
        var div = Math.Max(1, suspects.Count / MaxThreads);
        var chunks = suspects
            .Select((v, i) => new { v, i })
            .GroupBy(x => x.i / div)
            .Select(g => g.Select(x => x.v));
        Parallel.ForEach(chunks, c => {
            var btra = new List<FileRecord>();
            foreach (var x in c) {
                if (token.IsCancellationRequested) {
                    return;
                }
                // 日付やｻｲｽﾞが動いたものだけﾊｯｼｭ取り直し
                var it = FileTransaction.FromFile(source, x.Info);
                if (it == null) {
                    // 読めなかったものはｲﾝﾃﾞｯｸｽを触らず次回に
                    lock (failed) {
                        failed.Add(x.Record);
                    }
                    continue;
                }
                // 中身が同じ場合も日時は保存しとこうかな
                x.Record.Refresh(x.Info, it.Hash!);
                btra.Add(x.Record);
            }
            if (btra.Count == 0) {
                return;
            }
            lock (confirmed) {
                confirmed.AddRange(btra);
            }
        });
        foreach (var x in failed) {
            deferredFiles.Add(x);
        }
        if (token.IsCancellationRequested) {
            return failed.Count;
        }
        updated += confirmed.Count(w => w.IsUpdated);
        updatedFiles.AddRange(confirmed);
        return failed.Count;
    }

    /// <summary>
    /// 更新されたﾌｧｲﾙの解析結果を無効化して保存
    /// </summary>
    /// <param name="token"></param>
    void InvalidateUpdated(CancellationToken token) {
        if (token.IsCancellationRequested) {
            return;
        }
        // Refilterで落とされたものは後でPurgeされるので触らない
        var targets = updatedFiles.Where(w => w.IsAlive && !deferredFiles.Contains(w)).ToList();
        if (targets.Count == 0) {
            return;
        }
        OnMessage?.Invoke(Properties.Resources.CP_UpdateInProgress.Morfat(updated, targets.Count));
        var dirties = targets.Where(w => w.IsUpdated).ToList();
        if (dirties.Count != 0) {
            if (RecaptionUpdated) {
                foreach (var x in dirties) {
                    x.PendingRecaption = true;
                }
            }
            // 差し替えで読めるようになった可能性があるのでｴﾗー扱い解除
            IndexStore.Instance.RemoveErrors(dirties, token);
        }
        // 次回の起動で再解析される事を期待してここで保存しとく
        collective.更(IndexStore.Instance.Update(targets, false, token));
        OnMessage?.Invoke(Properties.Resources.CP_UpdateCompleted.Morfat(updated, targets.Count));
    }

    /// <summary>
    /// 画像ﾒﾀﾃﾞーﾀﾑとｻﾑﾈを取得
    /// </summary>
    /// <param name="token"></param>
    /// <returns></returns>
    async Task ProcessImages(CancellationToken token) {
        int c = 0;
        int errc = 0;
        var trans = new List<FileRecord>();
        var div = Math.Max(1, MaxThreads * ImageBufferCount);
        var chunks = filesIndex
            //.Shuffle()
            .Select((v, i) => new { v, i })
            .GroupBy(x => x.i / div)
            .Select(g => g.Select(x => x.v));
        var processThumb = Profile.Active.Thumbnail.Enable;

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

            var etra = new Dictionary<long, ErrorType>();
            int pid = 0;
            Parallel.ForEach(subchunks,
                new ParallelOptions { CancellationToken = token }, subchunk =>
            {
                var btra = new List<FileRecord>();
                int nid = 0;
                lock (trans) {
                    pid++;
                    nid = pid;
                }
                var thcp = (double)subchunk.Count();
                var lmcc = thcp;
                foreach (var x in subchunk) {
                    if (token.IsCancellationRequested) {
                        return;
                    }
                    Interlocked.Increment(ref c);
                    Interlocked.Decrement(ref subprog);
                    if ((c % MaxThreads) == 0) {
                        OnSubProgress?.Invoke(nid, subprog / (double)chunkCount);
                    }
                    lmcc++;
                    var isProcessed = x.Value.Version == SystemParams.VersionCurrent &&
                        (!processThumb || x.Value.HasThumbnail) &&
                        !x.Value.PendingRecaption && !imageReads.NeedRetry(x.Value.Id);
                    if (!x.Value.IsAlive || deferredFiles.Contains(x.Value)) {
                        continue;
                    }
                    if (isProcessed && !RescanMetadata) {
                        continue;
                    }
                    if ((c % MaxThreads) == 0) {
                        OnProgress?.Invoke(c, filesIndex.Count, errc, Properties.Resources.CP_Anyalize, x.Value.Path);
                    }
                    if (isProcessed) {
                        // 処理済みはﾒﾀﾃﾞーﾀﾑだけ取り直す
                        if (!ImageMetadataAnalyzer.TryAnalyze(sources, x.Value.Path, out var rmd, out var rex)) {
                            // 画像自体は読めているので読み込み失敗扱いにはしない
                            // 既存のﾒﾀﾃﾞーﾀを消さないようにﾚｺーﾄﾞは更新しない
                            Interlocked.Increment(ref errc);
                            OnError?.Invoke(rex, Properties.Resources.CP_E_AnyalizeMetadatumFailed.Morfat(x.Value.Path, rex.Message));
                            continue;
                        }
                        ApplyMetadata(x.Value, rmd);
                        btra.Add(x.Value);
                        continue;
                    }

                    using var img = imageReads.Load(x.Value, token);
                    if (img == null) {
                        lock (etra) {
                            etra[x.Value.Id] = ErrorType.File;
                        }
                        Interlocked.Increment(ref errc);
                        continue;
                    }
                    // 解析で例外が出てもこのﾌｧｲﾙだけｴﾗー扱いにして続行する
                    ImageMetadataDatum? tmd = null;
                    byte[]? thumbnail = null;
                    // ｻﾑﾈだけ作り直す系はﾒﾀﾃﾞーﾀﾑに触らない(書き直すと検索ｲﾝﾃﾞｯｸｽが無効になる)
                    var refreshMetadata = RescanMetadata || x.Value.Version != SystemParams.VersionCurrent;
                    try {
                        ImageHasher.CalcHash(img);
                        if (refreshMetadata && !ImageMetadataAnalyzer.TryAnalyze(sources, x.Value.Path, out tmd, out var tex)) {
                            // 画像は読めているのでﾒﾀﾃﾞーﾀﾑだけ諦める
                            // 既存のﾒﾀﾃﾞーﾀは残し、次のﾒﾀﾃﾞーﾀ再ｽｷｬﾝで取り直す
                            refreshMetadata = false;
                            Interlocked.Increment(ref errc);
                            OnError?.Invoke(tex, Properties.Resources.CP_E_AnyalizeMetadatumFailed.Morfat(x.Value.Path, tex.Message));
                        }
                        if (processThumb) {
                            thumbnail = ThumbnailEncoder.Encode(img.Bitmap, Profile.Active.Thumbnail.Size);
                        }
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
                        OnError?.Invoke(ex, Properties.Resources.CP_E_ImageAnyalizeFailed.Morfat(x.Value.Path, ex.Message));
                        continue;
                    }
                    // ここから先はﾚｺーﾄﾞへの反映だけ
                    if (refreshMetadata && tmd != null) {
                        ApplyMetadata(x.Value, tmd);
                    }
                    x.Value.Width = img.Bitmap.Width;
                    x.Value.Height = img.Bitmap.Height;

                    x.Value.PHash = img.PHash;
                    x.Value.AHash = img.AHash;
                    x.Value.DHash = img.DHash;
                    x.Value.WHash = img.WHash;

                    x.Value.Version = SystemParams.VersionCurrent;
                    if (processThumb) {
                        x.Value.Thumbnail = thumbnail;
                        x.Value.HasThumbnail = true;
                    }
                    else {
                        // 一時的に無効にしてｽｷｬﾝする用に保持するけど更新は走らせないﾏﾝ
                        // x.Value.HasThumbnail = false;
                        x.Value.Thumbnail = null;
                    }
                    x.Value.CaptionInvalidationReady = true;
                    btra.Add(x.Value);
                }
                if (btra.Count == 0) {
                    return;
                }
                lock (trans) {
                    trans.AddRange(btra);
                }
            });
            if (trans.Count > CommitFreq) {
                OnProgress?.Invoke(c, filesIndex.Count, errc, Properties.Resources.CP_Saving, trans.Last().Path);
                collective.更(IndexStore.Instance.Update(trans, true, token));
                foreach (var x in trans) {
                    x.Thumbnail = null;
                }
                trans.Clear();
            }
            imageReads.Flush(token);
            if (etra.Count != 0) {
                foreach (var t in etra) {
                    collective.更();
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
            collective.更(IndexStore.Instance.Update(trans, true, token));
            foreach (var x in trans) {
                x.Thumbnail = null;
            }
            trans.Clear();
        }
        OnSubProgress?.Invoke(-1, 0);
        if (!token.IsCancellationRequested) {
            if (filesIndex.Count != 0) {
                OnProgress?.Invoke(c, filesIndex.Count, errc, Properties.Resources.CP_Anyalize, filesIndex.Last().Value.Path);
            }
        }
    }

    /// <summary>
    /// 取り直したﾒﾀﾃﾞーﾀﾑをﾚｺーﾄﾞへ反映
    /// </summary>
    /// <param name="record"></param>
    /// <param name="datum"></param>
    static void ApplyMetadata(FileRecord record, ImageMetadataDatum datum) {
        record.MetadataChanged = true;
        if (record.MetadataDatum != null) {
            record.MetadataDatum.Merge(datum);
        }
        else {
            record.MetadataDatum = datum;
        }
        record.MetadataDatum.FileId = record.Id;
        if (datum.Timestamp != null) {
            // 作成日をﾒﾀﾃﾞーﾀﾑで上書き
            record.CreatedAt = datum.Timestamp.Value;
        }
    }

    /// <summary>
    /// ﾀｸﾞ付け
    /// </summary>
    /// <param name="token"></param>
    /// <returns></returns>
    async Task ProcessTagging(CancellationToken token)
    {
        if (!Profile.Active.Tagger.Enable) {
            return;
        }
        if (token.IsCancellationRequested) {
            return;
        }

        var tagger = TaggerObiekt.Default;
        if (tagger == null) {
            return;
        }
        var shape = tagger.Shape;
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(shape.Width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(shape.Height);

        int c = 0;
        int afc = 0;
        int errc = 0;
        var imageTrans = new List<ImageTransaction>();

        try {
            var div = Math.Max(1, MaxThreads * ImageBufferCount);
            var chunks = filesIndex
                .Select((v, i) => new { v, i })
                .GroupBy(x => x.i / div)
                .Select(g => g.Select(x => x.v));
            var afks = Math.Max(10, MaxThreads * 2);
            foreach (var chunk in chunks) {
                var chunkCount = chunk.Count();
                var subprog = chunkCount;
                foreach (var x in imageTrans) {
                    x.Dispose();
                }
                imageTrans.Clear();
                if (token.IsCancellationRequested) {
                    return;
                }
                var div2 = Math.Max(1, chunk.Count() / MaxThreads);
                var subchunks = chunk
                    .Select((v, i) => new { v, i })
                    .GroupBy(x => x.i / div2)
                    .Select(g => g.Select(x => x.v));

                int skipCount = 0;
                Parallel.ForEach(subchunks, new ParallelOptions { MaxDegreeOfParallelism = MaxThreads, CancellationToken = token }, subchunk => {
                    foreach (var x in subchunk) {
                        Interlocked.Increment(ref afc);
                        Interlocked.Decrement(ref subprog);
                        if ((afc % afks) == 0) {
                            OnProgress?.Invoke(afc, filesIndex.Count, errc, Properties.Resources.CP_Load, x.Value.Path);
                            OnSubProgress?.Invoke(0, subprog / (double)chunkCount);
                        }
                        if (token.IsCancellationRequested) {
                            return;
                        }
                        if (!x.Value.IsAlive || deferredFiles.Contains(x.Value) || x.Value.HasTag) {
                            Interlocked.Increment(ref skipCount);
                            continue;
                        }
                        // Camieは推論ｻｲｽﾞの数倍あれば足りるので縮小ﾃﾞｺーﾄﾞする
                        using var img = imageReads.Load(x.Value, token, tagger is CamieTagger
                            ? shape.Width * CamieTagger.PrescaleFactor : 0);
                        if (img == null) {
                            Interlocked.Increment(ref errc);
                            continue;
                        }
                        try {
                            // すんごいﾒﾓﾘ食うので、推論に必要なｻｲｽﾞだけ保持
                            var scale = Math.Min(1d, Math.Min(
                                (double)shape.Width / img.Bitmap.Width,
                                (double)shape.Height / img.Bitmap.Height));
                            var maxEdge = Math.Max(1, (int)Math.Floor(
                                Math.Max(img.Bitmap.Width, img.Bitmap.Height) * scale));
                            var resized = new ImageTransaction(tagger is CamieTagger
                                ? CamieTagger.PrepareImage(img.Bitmap, shape.Width)
                                : img.Bitmap.ResizeByMaxEdge(maxEdge)) {
                                File = x.Value
                            };
                            lock (imageTrans) {
                                imageTrans.Add(resized);
                            }
                        }
                        catch (Exception ex) {
                            Interlocked.Increment(ref errc);
                            OnError?.Invoke(ex, Properties.Resources.CP_E_ImagePreprocessFailed.Morfat(x.Value.Path, ex.Message));
                        }
                    }
                });
                if (token.IsCancellationRequested) {
                    return;
                }
                c += skipCount;

                // 推論
                var trans = new List<FileRecord>();
                subprog = imageTrans.Count;
                OnSubProgress?.Invoke(-1, 0);
                ParallelOptions parallelOptions = new ParallelOptions {
                    MaxDegreeOfParallelism = Profile.Active.Tagger.Threads.Positive()
                };
                var transChunks = imageTrans.Chunk(Profile.Active.Tagger.Threads.Positive());
                OnnxRuntimeException? ortexc = null;
                Parallel.ForEach(transChunks, parallelOptions, transChunk => {
                    var btra = new List<FileRecord>();
                    foreach (var x in transChunk) {
                        if (token.IsCancellationRequested) {
                            return;
                        }
                        if (ortexc != null) {
                            return;
                        }
                        Interlocked.Increment(ref c);
                        Interlocked.Decrement(ref subprog);
                        var pc = c / (double)filesIndex.Count;
                        if (x == null || x.File == null) {
                            continue;
                        }
                        if (!token.IsCancellationRequested && (subprog % 5) == 0) {
                            OnProgress?.Invoke(afc, filesIndex.Count, errc, Properties.Resources.CP_Tagging, x.File.Path);
                            OnSubProgress?.Invoke(0, subprog / (double)imageTrans.Count);
                        }
                        TaggingResult? pcr = null;
                        try {
                            pcr = tagger.Predict(x);
                            if (pcr == null) {
                                continue;
                            }
                        }
                        catch (OnnxRuntimeException ex) {
                            ortexc = ex;
                            return;
                        }
                        catch (Exception ex) {
                            Interlocked.Increment(ref errc);
                            OnError?.Invoke(ex, Properties.Resources.ZG_Error.Morfat(nameof(IndexProzessor), ex.Message));
                            continue;
                        }

                        x.File.HasTag = true;
                        x.File.Characters = string.Join(",", pcr.Character.Select(w => w.Name));
                        x.File.Tags = string.Join(",", pcr.Tags.Select(w => w.Name));
                        x.File.TaggingResult = pcr;
                        btra.Add(x.File);
                    }
                    lock (trans) {
                        trans.AddRange(btra);
                    }
                });
                if (ortexc != null) {
                    // onnxで何か起きた
                    throw new Exception(ortexc.Message, ortexc);
                }

                if (trans.Count != 0) {
                    collective.更(IndexStore.Instance.Update(trans, false, token));
                    trans.Clear();
                }
            }
        }
        finally {
            foreach (var x in imageTrans) {
                x.Dispose();
            }
            imageTrans.Clear();
        }
        OnProgress?.Invoke(filesIndex.Count, filesIndex.Count, errc, Properties.Resources.CP_Tagging, "");
    }

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
