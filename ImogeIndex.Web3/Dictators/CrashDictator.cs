using ImogeIndex.Common;
using System.Diagnostics;
using System.Text;
using System.Windows;
using System.Windows.Threading;

namespace ImogeIndex.Web3.Dictators;

/// <summary>
/// 未処理例外を拾ってｸﾗｯｼｭﾚﾎﾟーﾄを残す
/// </summary>
public static class CrashDictator
{
    static readonly object syncRoot = new();
    static Application? aidecation;
    static string aideName = nameof(ImogeIndex);
    static bool registered;
    static bool showingDialog;
    static bool exitRequested;
    static int faulted;

    /// <summary>
    /// ﾀﾞｲｱﾛｸﾞを出すか(自動実行ならfalseにしてﾛｸﾞだけ残して終了させる)
    /// </summary>
    public static bool Interactive { get; set; } = true;

    /// <summary>
    /// 未処理例外で終了することになったか(続行を選んだ分は含まない)
    /// </summary>
    public static bool HasFaulted => Volatile.Read(ref faulted) != 0;

    /// <summary>
    /// 未処理例外ﾊﾝﾄﾞﾗーを登録する
    /// </summary>
    /// <param name="app"></param>
    /// <param name="name"></param>
    public static void Register(Application app, string name)
    {
        ArgumentNullException.ThrowIfNull(app);
        lock (syncRoot) {
            if (registered) {
                return;
            }
            registered = true;
            aidecation = app;
            aideName = name;
        }
        CrashReporter.AppName = name;
        app.DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    }

    /// <summary>
    /// UIｽﾚｯﾄﾞの未処理例外(asyncのｷｬｯﾁ漏れもここに来る)
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        // 続行を選んだ時は正常扱いにしたいので、faultedは終了が決まった時(RequestExit)に立てる
        var reportPath = WriteReport("DispatcherUnhandledException", e.Exception);

        // ﾀﾞｲｱﾛｸﾞ表示中のﾒｯｾーｼﾞﾎﾟﾝﾌﾟで再発した分は記録だけして握る
        if (showingDialog || exitRequested) {
            e.Handled = true;
            return;
        }

        if (!Interactive || IsBurstError()) {
            e.Handled = true;
            RequestExit();
            return;
        }

        showingDialog = true;
        MessageBoxResult result;
        try {
            // GetForegroundWindow()でTaskDialog出したら酷い事になったので標準MessageBoxで
            result = MessageBox.Show(
                Properties.Resources.E_M_WantToContinue.Morfat(BuildMassage(e.Exception, reportPath)),
                Properties.Resources.E_M_UnexpectedErrorApr.Morfat(aideName),
                MessageBoxButton.YesNo,
                MessageBoxImage.Error,
                MessageBoxResult.No);
        }
        catch (Exception ex) {
            // ﾀﾞｲｱﾛｸﾞすら出せないなら終了させる
            WriteReport("DispatcherUnhandledException(Dialog)", ex);
            result = MessageBoxResult.No;
        }
        finally {
            showingDialog = false;
        }

        e.Handled = true;
        if (result != MessageBoxResult.Yes) {
            RequestExit();
        }
    }


    /// <summary>
    /// UIｽﾚｯﾄﾞ以外の未処理例外(ここに来たら続行不能なので記録だけ残す)
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    static void OnDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        Interlocked.Exchange(ref faulted, 1);
        var ex = e.ExceptionObject as Exception
            ?? new Exception(Properties.Resources.E_M_ExceptionObzekt.Morfat(e.ExceptionObject));
        var reportPath = WriteReport(
            e.IsTerminating ? "UnhandledException(Terminating)" : "UnhandledException",
            ex);

        if (!Interactive || !e.IsTerminating) {
            return;
        }
        try {
            // GetForegroundWindow()でTaskDialog出したら酷い事になったので標準MessageBoxで
            MessageBox.Show(
                Properties.Resources.E_M_EliminateApp.Morfat(BuildMassage(ex, reportPath)),
                Properties.Resources.E_M_FatalErrorApr.Morfat(aideName),
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        catch {
            // 終了間際なので何もできない
        }
    }

    /// <summary>
    // 握りつぶされたTaskの例外
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        WriteReport("UnobservedTaskException", e.Exception);
        e.SetObserved();
    }

    /// <summary>
    /// 短時間に例外が連発しているか
    /// </summary>
    static bool IsBurstError() => CrashReporter.IsBurstError();

    /// <summary>
    /// 通常の終了経路(MainWindowのClosing)で後始末させつつ終了する
    /// </summary>
    static void RequestExit()
    {
        Interlocked.Exchange(ref faulted, 1);
        if (exitRequested) {
            return;
        }
        exitRequested = true;
        var app = aidecation;
        if (app == null) {
            Environment.Exit(1);
            return;
        }
        try {
            // ﾊﾝﾄﾞﾗ内で閉じると例外元の処理と絡むので一旦抜けてから閉じる
            app.Dispatcher.BeginInvoke(() => {
                try {
                    if (app.MainWindow is { IsLoaded: true } window) {
                        // Closingで非同期の後始末をしてから閉じる窓もあるので、閉じ切ってから終了ｺーﾄﾞ付きで落とす
                        window.Closed += (_, _) => app.Shutdown(1);
                        // 後始末が固まって閉じ切らない場合に備えて、時間切れで強制的に落とす
                        StartExitWatchdog(app);
                        window.Close();
                    }
                    else {
                        app.Shutdown(1);
                    }
                }
                catch (Exception ex) {
                    WriteReport("RequestExit", ex);
                    app.Shutdown(1);
                }
            }, DispatcherPriority.Normal);
        }
        catch (Exception ex) {
            WriteReport("RequestExit", ex);
            Environment.Exit(1);
        }
    }

    /// <summary>
    /// 窓が閉じ切るまで待つ上限
    /// </summary>
    static readonly TimeSpan ExitTimeout = TimeSpan.FromSeconds(30);

    /// <summary>
    /// 時間内に終了しなければ強制終了する
    /// </summary>
    /// <param name="app"></param>
    static void StartExitWatchdog(Application app)
    {
        var timer = new DispatcherTimer(DispatcherPriority.Normal, app.Dispatcher) {
            Interval = ExitTimeout
        };
        timer.Tick += (_, _) => {
            timer.Stop();
            WriteReport("RequestExit", "終了処理が時間内に完了しなかったので強制終了");
            try {
                app.Shutdown(1);
            }
            catch (Exception ex) {
                WriteReport("RequestExit", ex);
                Environment.Exit(1);
            }
        };
        timer.Start();

        // UIｽﾚｯﾄﾞが固まってﾀｲﾏーが回らない場合の最終手段
        _ = Task.Delay(ExitTimeout * 2).ContinueWith(_ => Environment.Exit(1), TaskScheduler.Default);
    }

    /// <summary>
    /// ﾀﾞｲｱﾛｸﾞの本文
    /// </summary>
    static string BuildMassage(Exception ex, string? reportPath)
    {
        var sb = new StringBuilder();
        sb.AppendLine(Properties.Resources.E_M_UnexpectedError);
        sb.AppendLine();
        sb.Append(ex.GetType().Name).Append(": ").AppendLine(ex.Message);
        if (reportPath != null) {
            sb.AppendLine();
            sb.AppendLine(Properties.Resources.E_M_DetailsBelow);
            sb.Append(reportPath);
        }
        return sb.ToString().TrimEnd();
    }

    /// <summary>
    /// 処理済みのｴﾗーをﾚﾎﾟーﾄとして残す(ﾀﾞｲｱﾛｸﾞを出せない自動実行用)
    /// </summary>
    /// <param name="message"></param>
    /// <param name="ex"></param>
    /// <returns>書き出したﾊﾟｽ(失敗したらnull)</returns>
    public static string? Report(string message, Exception? ex = null)
    {
        var detail = ex == null ? message : message + Environment.NewLine + ex;
        return WriteReport("Reported", detail);
    }


    /// <summary>
    /// 例外のｸﾗｯｼｭﾚﾎﾟーﾄを書き出す
    /// </summary>
    /// <param name="source"></param>
    /// <param name="ex"></param>
    /// <returns>書き出したﾊﾟｽ(失敗したらnull)</returns>
    static string? WriteReport(string source, Exception ex) => CrashReporter.Write(source, ex);

    /// <summary>
    /// ｸﾗｯｼｭﾚﾎﾟーﾄを書き出す
    /// </summary>
    /// <param name="source"></param>
    /// <param name="detail"></param>
    /// <returns>書き出したﾊﾟｽ(失敗したらnull)</returns>
    static string? WriteReport(string source, string detail) => CrashReporter.Write(source, detail);
}