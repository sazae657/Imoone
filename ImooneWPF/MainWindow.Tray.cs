using System.ComponentModel;
using System.Windows;
using Forms = System.Windows.Forms;

namespace ImooneWPF;

// Windows.Formsの参照を混ぜたくねえので分離したﾄﾚｲｱｲｺﾝ関連
// (あまり肥大化したらDictatorに切り出す)
public partial class MainWindow
{
    Forms.NotifyIcon? trayIcon;
    Forms.ContextMenuStrip? trayMenu;
    System.Drawing.Icon? trayImage;
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
                trayIcon.Visible = false;
            }
            return;
        }
        if (!DC.MinimizeToTray)
        {
            return;
        }

        if (trayIcon == null)
        {
            trayImage = Properties.Embed.IDI_TRAYICON;
            trayMenu = new Forms.ContextMenuStrip();
            trayMenu.Items.Add(Properties.Resources.IDM_EXIT, null,
                (_, _) => Dispatcher.Invoke(Close));
            trayIcon = new Forms.NotifyIcon
            {
                Icon = trayImage,
                ContextMenuStrip = trayMenu
            };
            trayIcon.MouseClick += (_, args) =>
            {
                if (args.Button == Forms.MouseButtons.Left)
                {
                    Dispatcher.Invoke(RestoreFromTray);
                }
            };
        }
        UpdateTrayText();
        trayIcon.Visible = true;
        Hide();
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
        // 通知領域の文字数制限を超えないように丸める
        var text = Properties.Resources.MC_AppTitle;
        if (!string.IsNullOrEmpty(DC.ActiveProfile)) {
            text += " - " + DC.ActiveProfile.Replace('\r', ' ').Replace('\n', ' ');
        }
        if (text.Length > 63)
        {
            var length = char.IsHighSurrogate(text[61]) ? 61 : 62;
            text = text[..length] + "…";
        }
        trayIcon.Text = text;
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
        trayMenu?.Dispose();
        trayImage?.Dispose();
        trayIcon = null;
    }
}
