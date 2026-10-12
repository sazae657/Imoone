using ImogeIndex;
using ImogeIndex.Web3.Dictators;
using ImogeIndexWPF.Dictators;
using System.CommandLine;
using System.Data;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace ImogeIndexWPF;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    const string InstanceSemtexName = @"Local\ImogeIndexWPF.{F87F279A-6BE9-491A-B6F3-8BBB5231BFCC}.Web3";
    const string ActivateEventName = @"Local\ImogeIndexWPF.{261846B3-9FEB-4746-9170-A4524EDFA170}.Web3";

    Mutex? singleInstanceMutex;
    EventWaitHandle? activateEvent;
    // Closed後にApplication.MainWindowが外れても終了ｺーﾄﾞを拾えるよう自前で持つ
    MainWindow? mainView;
    RegisteredWaitHandle? activateEventRegistration;

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool ShowWindowAsync(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool AllowSetForegroundWindow(uint processId);

    const int SwRestore = 9;
    const uint AsfwAny = uint.MaxValue;

    /// <summary>
    /// 自動ﾓーﾄﾞ(--auto/--no-window)を簡易判定
    /// </summary>
    /// <param name="args"></param>
    /// <returns></returns>
    static bool IsNonInteractiveArgs(string[] args)
    {
        foreach (var arg in args) {
            if (arg == "--") {
                break;
            }
            if (arg.StartsWith("--", StringComparison.Ordinal)) {
                var name = arg.Split(':', '=')[0];
                if (name == "--auto" || name == "--no-window") {
                    return true;
                }
                continue;
            }
            // 短縮ｵﾌﾟｼｮﾝ判定
            if (arg.Length > 1 && arg[0] == '-') {
                foreach (var c in arg.AsSpan(1)) {
                    if (c == 'a' || c == 'n') {
                        return true;
                    }
                    if (c != 'q') {
                        // -p 以降は値なので見ない
                        break;
                    }
                }
            }
        }
        return false;
    }

    /// <summary>
    /// 起動時ｴﾗー表示(非対話時は記録のみ)
    /// </summary>
    /// <param name="message"></param>
    /// <param name="caption"></param>
    /// <param name="ex"></param>
    static void ShowStartupError(string message, string caption, Exception? ex)
    {
        if (!CrashDictator.Interactive) {
            CrashDictator.Report(message, ex);
            return;
        }
        MessageBox.Show(
            message,
            caption,
            MessageBoxButton.OK,
            MessageBoxImage.Error);
    }

    LaunchOptions? ProcessCommandline(StartupEventArgs e)
    {
        var autoOption = new Option<bool>("--auto", "-a") {
            Description = "自動",
            DefaultValueFactory = _ => false
        };

        var profileOption = new Option<string?>("--profile", "-p") {
            Description = "プロファイル名",
            DefaultValueFactory = _ => null
        };

        var multipleOption = new Option<bool>("--multiple") {
            Description = "ﾓーﾁﾎﾟー",
            DefaultValueFactory = _ => false
        };

        var quickScanOption = new Option<bool>("--quick-scan", "-q")
        {
            Description = "高速スキャン",
            DefaultValueFactory = _ => false
        };

        var noWinOption = new Option<bool>("--no-window", "-n") {
            Description = "ウインドウ表示しない",
            DefaultValueFactory = _ => false
        };

        var rootCommand = new RootCommand(nameof(ImogeIndexWPF));

        rootCommand.Options.Add(autoOption);
        rootCommand.Options.Add(profileOption);
        rootCommand.Options.Add(multipleOption);
        rootCommand.Options.Add(quickScanOption);
        rootCommand.Options.Add(noWinOption);           

        // StartupEventArgs.Argsには実行ﾌｧｲﾙ名を除いた引数が入っている
        var parseResult = rootCommand.Parse(e.Args);

        if (parseResult.Errors.Count > 0) {
            var message = string.Join(
                Environment.NewLine,
                parseResult.Errors.Select(error => error.Message));

            ShowStartupError(message, ImogeIndexWPF.Properties.Resources.WG_E_InvalidArgument, null);
            return null;
        }
        return new LaunchOptions(
            Auto: parseResult.GetValue(autoOption),
            Profile: parseResult.GetValue(profileOption),
            AllowMultiple: parseResult.GetValue(multipleOption),
            QuickScan: parseResult.GetValue(quickScanOption),
            NoWindow: parseResult.GetValue(noWinOption)
        );

    }

    protected override void OnStartup(StartupEventArgs e)
    {
        // ｸﾗｯｼｭﾊﾝﾄﾞﾗーは最優先で入れとく
        CrashDictator.Register(this, nameof(ImogeIndexWPF));
        CrashDictator.Interactive = !IsNonInteractiveArgs(e.Args);
        base.OnStartup(e);
        try {
            SystemParams.Sanitize();
        }
        catch (Exception ex) {
            ShowStartupError(ex.Message, ImogeIndexWPF.Properties.Resources.WG_Eroare, ex);
            Shutdown((int)ExitStatus.Failure);
            return;
        }

        // ｺﾏﾝﾄﾞﾗｲﾝのｴﾗー表示より前にﾘｿーｽの言語を確定させる
        WarpSettings.Load();
        LanguageDictator.Apply(WarpSettings.Default.Language);

        var options = ProcessCommandline(e);
        if (options == null) {
            // ｺﾏﾝﾄﾞﾗｲﾝしくじってるので終了
            Shutdown((int)ExitStatus.InvalidArguments);
            return;
        }
        // 自動実行中はﾀﾞｲｱﾛｸﾞで止まらないようにﾛｸﾞだけ残して終了させる
        CrashDictator.Interactive = !(options.Auto || options.NoWindow);

        if (!options.AllowMultiple) {
            singleInstanceMutex = new Mutex(true, InstanceSemtexName, out var isFirstInstance);
            if (!isFirstInstance) {
                AllowSetForegroundWindow(AsfwAny);
                NotifyRunningInstance();
                singleInstanceMutex.Dispose();
                singleInstanceMutex = null;
                Shutdown((int)ExitStatus.AlreadyRunning);
                return;
            }

            activateEvent = new EventWaitHandle(
                false,
                EventResetMode.AutoReset,
                ActivateEventName);
            activateEventRegistration = ThreadPool.RegisterWaitForSingleObject(
                activateEvent,
                (_, _) => Dispatcher.BeginInvoke(ActivateMainWindow),
                null,
                Timeout.Infinite,
                false);
        }
        var theme = ThemeDisplay.StringToTheme(WarpSettings.Default.Theme);
        ThemeDictator.ApplyTheme(theme);
        var window = new MainWindow(options);
        mainView = window;
        MainWindow = window;
        window.Show();
    }

    static void NotifyRunningInstance()
    {
        // 最初のｲﾝｽﾀﾝｽがｲﾍﾞﾝﾄを準備している間だけちょい待つ
        for (var retry = 0; retry < 20; retry++) {
            try {
                using var existingEvent = EventWaitHandle.OpenExisting(ActivateEventName);
                existingEvent.Set();
                return;
            }
            catch (WaitHandleCannotBeOpenedException) {
                Thread.Sleep(50);
            }
        }
    }

    void ActivateMainWindow()
    {
        var window = MainWindow;
        if (window == null) {
            return;
        }

        if (window is ImogeIndexWPF.MainWindow mainWindow) {
            mainWindow.RestoreFromTray();
        }
        else if (window.WindowState == WindowState.Minimized)
        {
            window.WindowState = WindowState.Normal;
        }

        window.Show();
        var handle = new WindowInteropHelper(window).Handle;

        SetForegroundWindow(handle);
        window.Activate();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (CrashDictator.HasFaulted) {
            e.ApplicationExitCode = (int)ExitStatus.Failure;
        }
        else if (e.ApplicationExitCode == 0 && mainView != null) {
            // Shutdown(n)で明示されてなければ処理結果を返す
            e.ApplicationExitCode = (int)mainView.ExitStatus;
        }
        activateEventRegistration?.Unregister(null);
        activateEvent?.Dispose();
        singleInstanceMutex?.ReleaseMutex();
        singleInstanceMutex?.Dispose();
        base.OnExit(e);
    }
}
