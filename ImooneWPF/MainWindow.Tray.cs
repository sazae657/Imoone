using System.ComponentModel;
using System.IO;
using System.Windows;
using ImogeIndex.Web6.Ganadores;

namespace ImooneWPF;

// ﾀｽｸﾄﾚｲ関連
public partial class MainWindow
{
    NiraTrayIcon? trayIcon;
    WindowState restoreWindowState = WindowState.Normal;

    /// <summary>
    /// ﾀｽｸﾄﾚｲ関連
    /// </summary>
    void InitializeTrayIcon()
    {
        StateChanged += Window_StateChanged;
        DC.PropertyChanged += TrayModel_PropertyChanged;
    }

    private void Window_StateChanged(object? sender, EventArgs e)
    {
        if (wasClosing)
        {
            return;
        }
        if (WindowState != WindowState.Minimized)
        {
            restoreWindowState = WindowState;
            if (trayIcon != null)
            {
                trayIcon.Hide();
            }
            return;
        }
        if (!DC.MinimizeToTray)
        {
            return;
        }

        if (trayIcon == null)
        {
            try
            {
                trayIcon = new NiraTrayIcon(
                    Path.Combine(AppContext.BaseDirectory, "Assets", "Imoone.ico"), Properties.Resources.IDM_EXIT);
                trayIcon.RestoreRequested += () => Dispatcher.BeginInvoke(new Action(RestoreFromTray));
                trayIcon.ExitRequested += () => Dispatcher.BeginInvoke(new Action(Close));
                trayIcon.RegistrationLost += () => Dispatcher.BeginInvoke(new Action(RestoreFromTray));
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                System.Diagnostics.Trace.TraceError(ex.ToString());
                return;
            }
        }
        UpdateTrayText();
        if (trayIcon.TryShow())
        {
            Hide();
        }
    }

    private void TrayModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(Modelle.MainWindowModell.ActiveProfile))
        {
            UpdateTrayText();
        }
    }

    /// <summary>
    /// ﾄﾚｲｱｲｺﾝ
    /// </summary>
    void UpdateTrayText()
    {
        if (trayIcon == null)
        {
            return;
        }
        trayIcon.UpdateText(Properties.Resources.MC_AppTitle, DC.ActiveProfile);
    }

    /// <summary>
    /// ﾄﾚｲから復元
    /// </summary>
    internal void RestoreFromTray()
    {
        if (wasClosing)
        {
            return;
        }
        trayIcon?.Hide();
        Show();
        WindowState = restoreWindowState;
        Activate();
    }

    /// <summary>
    /// ﾀｽｸﾄﾚｲｱｲｺﾝの破棄
    /// </summary>
    void DisposeTrayIcon()
    {
        StateChanged -= Window_StateChanged;
        DC.PropertyChanged -= TrayModel_PropertyChanged;
        trayIcon?.Dispose();
        trayIcon = null;
    }
}
