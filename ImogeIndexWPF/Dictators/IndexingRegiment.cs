using ImogeIndex;
using ImogeIndex.Captionizers;
using ImogeIndex.Common;
using ImogeIndex.Properties;
using ImogeIndex.Storage;
using ImogeIndex.Taggers;
using ImogeIndex.Territories;
using ImogeIndex.Zenerators;
using ImogeIndexWPF.Modelle;
using System.Diagnostics;

namespace ImogeIndexWPF.Dictators;

/// <summary>
/// 実行ごとに画面から受け取るｵﾌﾟｼｮﾝ
/// </summary>
/// <param name="RescanMetadata">ﾒﾀﾃﾞーﾀを再ｽｷｬﾝする</param>
/// <param name="QuickScan">ｸｲｯｸｽｷｬﾝ</param>
/// <param name="RecaptionUpdated">更新されたﾌｧｲﾙをｷｬﾌﾟｼｮﾝし直す</param>
/// <param name="SkipKnownError">既知のｴﾗーを飛ばす</param>
/// <param name="RethrowErrors">ｴﾗーを表示せず呼び出し元へ投げる(自動実行用)</param>
internal sealed record IndexingOptions(
    bool RescanMetadata,
    bool QuickScan,
    bool RecaptionUpdated,
    bool SkipKnownError,
    bool RethrowErrors);

/// <summary>
/// ﾌﾟﾛｾｯｻーを実行計画に従って実行
/// </summary>
internal sealed class IndexingRegiment : IDisposable
{
    readonly IIndexingHost host;
    readonly IndexerStateDictator state;

    readonly IndexProzessor indexProcessor = new();
    readonly ThumbnailProzessor thumbnailProcessor = new();
    readonly TagIndexProzessor tagIndexProcessor = new();
    readonly CaptionProzessor captionProcessor = new();
    readonly List<IProzessor> prozessoren;

    CancellationTokenSource? tokenSource;

    public IndexingRegiment(IIndexingHost host, IndexerStateDictator state)
    {
        this.host = host;
        this.state = state;

        indexProcessor.OnMessage += host.PostMessage;
        indexProcessor.OnProgress += host.PostProgress;
        indexProcessor.OnSubProgress += host.PostSubProgress;
        indexProcessor.OnError += Prozessor_OnError;

        thumbnailProcessor.OnMessage += host.PostMessage;
        thumbnailProcessor.OnProgress += host.PostProgress;
        thumbnailProcessor.OnSubProgress += host.PostSubProgress;
        thumbnailProcessor.OnError += Prozessor_OnError;

        tagIndexProcessor.OnMessage += host.PostMessage;
        tagIndexProcessor.OnProgress += host.PostProgress;
        tagIndexProcessor.OnError += Prozessor_OnError;

        captionProcessor.OnMessage += host.PostMessage;
        captionProcessor.OnProgress += host.PostProgress;
        captionProcessor.OnError += Prozessor_OnError;

        WmlStrapper.OnProgress += host.PostProgress;
        WmlStrapper.OnMessage += host.PostMessage;
        TaggerObiekt.OnProgress += host.PostProgress;
        TaggerObiekt.OnMessage += host.PostMessage;

        Captionizer.CaptionRecieved += host.PostCaption;

        prozessoren = [indexProcessor, thumbnailProcessor, tagIndexProcessor, captionProcessor];
        prozessoren.ForEach(w => {
            // ﾌﾟﾛｾｯｻーはｽﾚｯﾄﾞﾌﾟーﾙから
            w.BeginVeryLongProcess += () => host.PostTremendouslyLongProcess(true);
            w.EndVeryLongProcess += () => host.PostTremendouslyLongProcess(false);
        });
    }

    /// <summary>
    /// 停止要求
    /// </summary>
    public void Cancel()
    {
        tokenSource?.Cancel();
    }

    /// <summary>
    /// 進軍計画に従ってﾌﾟﾛｾｯｻー実行
    /// </summary>
    /// <param name="plan"></param>
    /// <param name="options"></param>
    /// <returns></returns>
    public async Task<ExitStatus> Advance(ProcessPlan plan, IndexingOptions options)
    {
        try {
            var token = RenewToken();
            if (!await PrepareProcess(options, token)) {
                return ExitStatus.Failure;
            }

            // ｲﾝﾃﾞｯｸｽ生成
            var indexModified = false;
            if (plan.Index) {
                var irc = await RunIndexProcessor(token);
                if (irc.WasFailed()) {
                    return ExitStatus.Failure;
                }
                if (irc.Cancelled) {
                    return await FinishInterruptedIndexing(irc.Collective.WasModified);
                }
                indexModified = irc.Collective.WasModified;
            }

            // 検索ｲﾝﾃﾞｯｸｽ生成
            if (plan.TagIndex || indexModified) {
                await RunTagIndexProcessor(token);
            }
            host.ShowProgress(1.0);

            // Taggerもう要らない
            TaggerObiekt.Terminate();
            if (plan.Vacuum != VacuumMode.None) {
                host.ShowMessage(Properties.Resources.TB_CleanupDb);
                host.SetBusy(true);
                await ThumbnailStore.Instance.Vacuum(plan.Vacuum == VacuumMode.Full, token);
                host.SetBusy(false);
            }
            ThumbnailStore.Close();
            if (indexModified || plan.NotifyAlways) {
                // ThumbnailStore閉じた時点でｲﾝﾃﾞｯｸｽ更新は終わり、変更があったらｲﾓｹﾞｼﾝｸﾞへ通知
                await IndexingCompletionNotifier.NotifyAsync(
                    Profile.Dictator.ActiveProfile, CancellationToken.None);
            }

            // ｷｬﾌﾟｼｮﾆﾝｸﾞ
            var captionModified = false;
            if (plan.Captioning) {
                var crc = await RunCaptionProcessor(indexProcessor.Files, token);
                if (crc.WasFailed()) {
                    return ExitStatus.Failure;
                }
                host.ShowMessage(Properties.Resources.CP_Ended.Morfat(crc.Found));
                captionModified = crc.Collective.WasModified;
            }

            if (state.IsClosing) {
                return ExitStatus.Cancelled;
            }
            var interrupted = tokenSource!.IsCancellationRequested;
            if (interrupted) {
                // ｷｬﾌﾟｼｮﾆﾝｸﾞ中断されたけど、検索ｲﾝﾃﾞｯｸｽ構築だけはやる(整理はしない)
                token = RenewToken();
            }
            if (captionModified) {
                await RunTagIndexProcessor(token);
            }

            if (plan.Vacuum != VacuumMode.None && !interrupted) {
                host.ShowMessage(Properties.Resources.IP_CleanupDb);
                host.ShowProgress(1.0);
                host.SetBusy(true);
                await IndexStore.Instance.Vacuum(plan.Vacuum == VacuumMode.Full, token);
                host.SetBusy(false);
            }

            host.ShowMessage(state.IsStopping ? Properties.Resources.PM_Interrupted : Properties.Resources.PM_Ended);
            return state.IsStopping ? ExitStatus.Cancelled : ExitStatus.Success;
        }
        catch (OperationCanceledException) when (tokenSource?.IsCancellationRequested == true) {
            host.ShowMessage(Properties.Resources.PM_Stopped);
            return ExitStatus.Cancelled;
        }
        catch (Exception ex) {
            if (options.RethrowErrors) {
                throw;
            }
            host.ReportError(ex.Message, ex);
            host.ShowMessage(Properties.Resources.PM_Error.Morfat(ex.Message));
            return ExitStatus.Failure;
        }
        finally {
            CleanupProcess();
        }
    }

    /// <summary>
    /// 終了時の後片付け
    /// 実行中の処理が終わってから呼ぶこと
    /// </summary>
    /// <returns></returns>
    public async Task Withdrawal()
    {
        foreach (var g in prozessoren) {
            host.ShowMessage(Properties.Resources.PM_StoppingProcessor.Morfat(g.GetType().Name));
            try {
                await g.WaitFor();
            }
            catch (Exception ex) {
                ClosingStep.Report($"{g.GetType().Name}.WaitFor", ex);
            }
            ClosingStep.Run($"{g.GetType().Name}.Dispose", g.Dispose);
        }
        ClosingStep.Run("SetBusy", () => host.SetBusy(false));
        host.ShowMessage(Properties.Resources.PM_Closing);
        await Task.Run(() => {
            ClosingStep.Run("IndexStore", IndexStore.Close);
            ClosingStep.Run("ThumbnailStore", ThumbnailStore.Close);
            ClosingStep.Run("TaggerObiekt", TaggerObiekt.Terminate);
            ClosingStep.Run("WmlStrapper", WmlStrapper.Terminate);
            ClosingStep.Run("Logger", Logger.Dictator.Close);
        });
    }

    /// <summary>
    /// staticなｲﾍﾞﾝﾄの購読を解除してﾄーｸﾝを捨てる
    /// </summary>
    public void Dispose()
    {
        WmlStrapper.OnProgress -= host.PostProgress;
        WmlStrapper.OnMessage -= host.PostMessage;
        TaggerObiekt.OnProgress -= host.PostProgress;
        TaggerObiekt.OnMessage -= host.PostMessage;
        Captionizer.CaptionRecieved -= host.PostCaption;
        DisposeToken();
    }

    /// <summary>
    /// 停止用ﾄーｸﾝを捨てる
    /// </summary>
    public void DisposeToken()
    {
        tokenSource?.Dispose();
        tokenSource = null;
    }

    /// <summary>
    /// 停止用ﾄーｸﾝを作り直す
    /// </summary>
    /// <returns></returns>
    CancellationToken RenewToken()
    {
        tokenSource?.Dispose();
        tokenSource = new();
        return tokenSource.Token;
    }

    /// <summary>
    /// ﾌﾟﾛｾｽ実行準備
    /// </summary>
    /// <returns></returns>
    async Task<bool> PrepareProcess(IndexingOptions options, CancellationToken token)
    {
        Profile.Reload();
        // Tagger確認
        if (!TaggerObiekt.CheckConfiguration(Profile.Active)) {
            host.ReportTaggerMisconfiguration();
            return false;
        }

        host.ShowMessage(Properties.Resources.PM_InPreparation);
        state.AllowStop();
        await Sanitize(options, token);
        return true;
    }

    /// <summary>
    /// 実行前処理
    /// </summary>
    /// <returns></returns>
    async Task Sanitize(IndexingOptions options, CancellationToken token)
    {
        Logger.Dictator.Open(Profile.Active.Root.CombineDir("Logs"));
        await Task.Run(() => {
            try {
                IndexStore.Oppen(true);
                ThumbnailStore.Oppen(true);
            }
            catch {
                // 片方だけ開いた状態を残さない
                IndexStore.Close();
                ThumbnailStore.Close();
                throw;
            }
        });
        token.ThrowIfCancellationRequested();
        indexProcessor.MaxThreads = Profile.Active.Threads.Positive();
        indexProcessor.ImageBufferCount = Profile.Active.BufferCount.Positive();
        indexProcessor.RescanMetadata = options.RescanMetadata;
        indexProcessor.QuickScan = options.QuickScan;
        indexProcessor.RecaptionUpdated = options.RecaptionUpdated;
        thumbnailProcessor.MaxThreads = Profile.Active.Threads.Positive();
        thumbnailProcessor.ImageBufferCount = Profile.Active.BufferCount.Positive();
        captionProcessor.SkipKnownError = options.SkipKnownError;
        if (Profile.Active.Tagger.Enable) {
            host.ShowMessage(Properties.Resources.PM_LoadingTagger);
            TaggerObiekt.Terminate();
            await WmlStrapper.Sanitize(true);
            // WML初期化自体は中断できないので終わった所で確認する
            token.ThrowIfCancellationRequested();
            var ccm = Profile.Active.CacheDirectory;
            await TaggerObiekt.Sanitize(Profile.Active, ccm, token);
            host.ShowMessage(Properties.Resources.PM_TaggerLoaded);
        }
        token.ThrowIfCancellationRequested();
        Captionizer.Contaminate();
        if (Profile.Active.CaptioningEnabled) {
            Captionizer.Sanitize(Profile.Active, Profile.Active.Captioning);
            host.ShowMessage(Properties.Resources.PM_CheckingVLM);
            var cap = Captionizer.Obiekt<ICaptionizer>() ??
                throw new Exception(Properties.Resources.PM_E_CaptionizerNotSanitized);
            await cap.Verify(token);
            host.ShowMessage(Properties.Resources.PM_VLMChecked);
        }
    }

    /// <summary>
    /// 実行後の後片付け
    /// </summary>
    void CleanupProcess()
    {
        host.SetBusy(false);
        host.ShowProgress(0);
        ThumbnailStore.Close();
        IndexStore.Close();
        host.ResetSubProgress();
        DisposeToken();
    }

    /// <summary>
    /// ｲﾝﾃﾞｯｸｽ生成を中断した後の後処理
    /// </summary>
    /// <param name="modified">中断までにｲﾝﾃﾞｯｸｽが更新された？</param>
    /// <returns></returns>
    async Task<ExitStatus> FinishInterruptedIndexing(bool modified)
    {
        TaggerObiekt.Terminate();
        ThumbnailStore.Close();
        if (modified) {
            await IndexingCompletionNotifier.NotifyAsync(
                Profile.Dictator.ActiveProfile, CancellationToken.None);
        }
        if (state.IsClosing || !modified) {
            host.ShowMessage(Properties.Resources.PM_Stopped);
            return ExitStatus.Cancelled;
        }
        // 停止要求済みのﾄーｸﾝでは何もできないので作り直す
        var rc = await RunTagIndexProcessor(RenewToken());
        host.ShowMessage(Properties.Resources.PM_Interrupted); // { rc.Groups} {rc.Characters}");
        return ExitStatus.Cancelled;
    }

    /// <summary>
    /// ﾒｲﾝのｲﾝﾃﾞｯｸｽ生成
    /// </summary>
    /// <param name="token"></param>
    /// <returns></returns>
    async Task<IndexingResult> RunIndexProcessor(CancellationToken token)
    {
        var rc = await indexProcessor.Run(token);
        host.ShowMessage(Properties.Resources.IP_Ended.Morfat(rc.Found));
        if (rc.WasFailed()) {
            if (rc.Exception != null) {
                Debug.WriteLine($"{rc.Exception.Message}\n{rc.Exception.StackTrace}");
            }
            host.ReportError(Properties.Resources.PM_Error.Morfat(rc.Exception?.Message), rc.Exception);
            host.ShowMessage(Properties.Resources.PM_Error.Morfat(rc.Exception?.Message));
        }
        return rc;
    }

    /// <summary>
    /// 検索ｲﾝﾃﾞｯｸｽ生成
    /// </summary>
    /// <param name="token"></param>
    /// <returns></returns>
    async Task<TagIndexingResult> RunTagIndexProcessor(CancellationToken token)
    {
        var rc = await tagIndexProcessor.Run(token);
        if (rc.WasFailed()) {
            throw rc.Exception ?? new InvalidOperationException(Properties.Resources.IP_E_Fail);
        }
        host.ShowMessage(Properties.Resources.IP_TagIndexEnded.Morfat(rc.Groups, rc.Characters));
        return rc;
    }

    /// <summary>
    /// ｷｬﾌﾟｼｮﾆﾝｸﾞ
    /// </summary>
    /// <param name="files"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    async Task<IndexingResult> RunCaptionProcessor(IEnumerable<FileRecord> files, CancellationToken token)
    {
        var rc = await captionProcessor.Run(files, token);
        if (rc.WasFailed()) {
            host.ReportError(Properties.Resources.PM_Error.Morfat(rc.Exception?.Message), rc.Exception);
            host.ShowMessage(Properties.Resources.PM_Error.Morfat(rc.Exception?.Message));
        }
        return rc;
    }

    /// <summary>
    /// ﾌﾟﾛｾｯｻーのｴﾗーを記録する
    /// </summary>
    /// <param name="e"></param>
    /// <param name="message"></param>
    void Prozessor_OnError(Exception e, string message)
    {
        Logger.Dictator.Error(e, message);
        Debug.WriteLine($"Generator_OnError: {e.Message} {message}\n{e.StackTrace}");
    }
}
