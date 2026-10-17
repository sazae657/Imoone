using ImogeIndex.Web6.Ganadores;
using System.ComponentModel;
using System.IO;
using System.Windows.Threading;


namespace ImogeIndexWPF.Dictators;

/// <summary>
/// ﾀｽｸﾄﾚｲのｱｲｺﾝ制御
/// </summary>
internal sealed class TrayIconDictator : IDisposable
{
    readonly Dispatcher dispatcher;
    readonly Action restore;
    readonly IReadOnlyList<TrayMenuCommand?> commands;
    readonly DispatcherTimer timer;
    readonly Func<string> getStatus;
    NiraTrayIcon? icon;
    bool canStart;
    bool canStop;
    bool canExit;
    bool disposed;

    public TrayIconDictator(Dispatcher dispatcher, Action restore, Action startAction,
        Action stopAction, Action exitAction, Func<string> getStatus)
    {
        this.dispatcher = dispatcher;
        this.restore = restore;
        this.getStatus = getStatus;
        commands = [
            new TrayMenuCommand(Properties.Resources.IDM_START,
                () => QueueCommand(startAction, () => canStart), () => canStart),
            new TrayMenuCommand(Properties.Resources.IDM_STOP,
                () => QueueCommand(stopAction, () => canStop), () => canStop),
            null,
            new TrayMenuCommand(Properties.Resources.IDM_EXIT,
                () => QueueCommand(exitAction, () => canExit), () => canExit)
        ];
        timer = new DispatcherTimer(TimeSpan.FromMilliseconds(500), DispatcherPriority.Background,
            (_, _) => UpdateText(), dispatcher);
        timer.Stop();
    }

    void QueueCommand(Action action, Func<bool> enabled)
    {
        dispatcher.BeginInvoke(new Action(() =>
        {
            if (!disposed && enabled())
            {
                action();
            }
        }));
    }

    void QueueRestore()
    {
        dispatcher.BeginInvoke(new Action(() =>
        {
            if (!disposed)
            {
                restore();
            }
        }));
    }

    public void SetCommands(bool canStart, bool canStop, bool canExit)
    {
        this.canStart = canStart;
        this.canStop = canStop;
        this.canExit = canExit;
    }

    public bool TryShow()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (icon == null)
        {
            try
            {
                icon = new NiraTrayIcon(
                    Path.Combine(AppContext.BaseDirectory, "Assets", "ImogeIndex.ico"), commands);
                icon.RestoreRequested += QueueRestore;
                icon.RegistrationLost += OnRegistrationLost;
            }
            catch (Win32Exception ex)
            {
                System.Diagnostics.Trace.TraceError(ex.ToString());
                return false;
            }
        }
        UpdateText();
        if (!icon.TryShow())
        {
            return false;
        }
        timer.Start();
        return true;
    }

    void OnRegistrationLost()
    {
        timer.Stop();
        QueueRestore();
    }

    public void Hide()
    {
        timer.Stop();
        icon?.Hide();
    }

    void UpdateText() => icon?.UpdateText(getStatus(), null);

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }
        disposed = true;
        timer.Stop();
        icon?.Dispose();
        icon = null;
    }
}
