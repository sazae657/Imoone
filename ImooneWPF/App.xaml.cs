using ImogeIndex;
using ImogeIndex.Web3.Dictators;
using ImooneWPF.Dictators;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace ImooneWPF;

/// <summary>
/// ｱﾌﾟﾘｹーｼｮﾝの起動と終了
/// </summary>
public partial class App : Application {
    const string InstanceMutexName = @"Local\ImooneWPF.{E8820D29-8731-4C10-97B8-65D7888801DB}.web3";
    const string ActivateEventName = @"Local\ImooneWPF.{AA7A3E13-513E-49CB-86A7-D3E827230015}.web3";
    const uint AsfwAny = uint.MaxValue;

    Mutex? singleInstanceMutex;
    EventWaitHandle? activateEvent;
    RegisteredWaitHandle? activateEventRegistration;

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool AllowSetForegroundWindow(uint processId);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool SetForegroundWindow(IntPtr hWnd);

    protected override void OnStartup(StartupEventArgs e)
    {
        // 起動処理中の例外も拾えるよう最初に登録する
        CrashDictator.Register(this, nameof(ImooneWPF));

        // 通知取りこぼし対策で排他制御より先にｲﾍﾞﾝﾄを用意しとく
        activateEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ActivateEventName);
        singleInstanceMutex = new Mutex(false, InstanceMutexName);
        bool ownsMutex;
        try {
            ownsMutex = singleInstanceMutex.WaitOne(0);
        }
        catch (AbandonedMutexException) {
            // たぶんｺｹたやつが残ってる
            ownsMutex = true;
        }
        if (!ownsMutex)
        {
            AllowSetForegroundWindow(AsfwAny);
            activateEvent.Set();
            singleInstanceMutex.Dispose();
            singleInstanceMutex = null;
            Shutdown();
            return;
        }

        base.OnStartup(e);
        try {
            SystemParams.Sanitize();
        }
        catch (Exception ex) {
            MessageBox.Show(
                ex.Message,
                ImooneWPF.Properties.Resources.MC_Error,
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(1);
            return;
        }

        SettingsDictator.Load();
        // ｳｲﾝﾄﾞｳ生成前にﾘｿーｽの言語を確定させる
        LanguageDictator.Apply(SettingsDictator.Default.Language);
        var theme = ThemeDisplay.StringToTheme(SettingsDictator.Default.Theme);
        ThemeDictator.ApplyTheme(theme);
        var window = new MainWindow();
        MainWindow = window;
        window.Show();

        // 起動中に届いた通知を捌く
        activateEventRegistration = ThreadPool.RegisterWaitForSingleObject(
            activateEvent,
            (_, _) => Dispatcher.BeginInvoke(ActivateMainWindow),
            null,
            Timeout.Infinite,
            false);
    }

    void ActivateMainWindow()
    {
        if (Dispatcher.HasShutdownStarted || MainWindow is not MainWindow window)
        {
            return;
        }
        window.RestoreFromTray();
        SetForegroundWindow(new WindowInteropHelper(window).Handle);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        activateEventRegistration?.Unregister(null);
        activateEvent?.Dispose();
        singleInstanceMutex?.ReleaseMutex();
        singleInstanceMutex?.Dispose();
        base.OnExit(e);
    }
}
