using System.Windows.Threading;
using Forms = System.Windows.Forms;

namespace ImogeIndexWPF.Dictators;

/// <summary>
/// ﾀｽｸﾄﾚｲのｱｲｺﾝ制御
/// </summary>
internal sealed class TrayIconDictator : IDisposable
{
    readonly Forms.NotifyIcon icon;
    readonly Forms.ContextMenuStrip menu = new();
    readonly Forms.ToolStripMenuItem start;
    readonly Forms.ToolStripMenuItem stop;
    readonly Forms.ToolStripMenuItem exit;
    readonly DispatcherTimer timer;
    readonly Func<string> getStatus;
    private bool disposedValue;

    public TrayIconDictator(Dispatcher dispatcher, Action restore, Action startAction,
        Action stopAction, Action exitAction, Func<string> getStatus)
    {
        this.getStatus = getStatus;
        start = new Forms.ToolStripMenuItem(Properties.Resources.IDM_START);
        stop = new Forms.ToolStripMenuItem(Properties.Resources.IDM_STOP);
        exit = new Forms.ToolStripMenuItem(Properties.Resources.IDM_EXIT);
        start.Click += (_, _) => dispatcher.Invoke(startAction);
        stop.Click += (_, _) => dispatcher.Invoke(stopAction);
        exit.Click += (_, _) => dispatcher.Invoke(exitAction);
        menu.Items.AddRange([start, stop, new Forms.ToolStripSeparator(), exit]);
        icon = new Forms.NotifyIcon {
            Icon = Properties.Embed.IDI_TRAYICON,
            ContextMenuStrip = menu
        };
        icon.MouseClick += (_, e) => {
            if (e.Button == Forms.MouseButtons.Left) {
                dispatcher.Invoke(restore);
            }
        };
        timer = new DispatcherTimer(TimeSpan.FromMilliseconds(500), DispatcherPriority.Background,
            (_, _) => UpdateText(), dispatcher);
        timer.Stop();
    }

    public void SetCommands(bool canStart, bool canStop, bool canExit)
    {
        start.Enabled = canStart;
        stop.Enabled = canStop;
        exit.Enabled = canExit;
    }

    public void Show()
    {
        UpdateText();
        icon.Visible = true;
        timer.Start();
    }

    public void Hide()
    {
        timer.Stop();
        icon.Visible = false;
    }

    void UpdateText()
    {
        // 通知領域の文字数制限を超えないように丸める
        var text = getStatus().Replace('\r', ' ').Replace('\n', ' ');
        if (text.Length > 63) {
            var length = char.IsHighSurrogate(text[61]) ? 61 : 62;
            text = text[..length] + "…";
        }
        if (icon.Text != text) {
            icon.Text = text;
        }
    }

    #region IDisposable

    private void Dispose(bool disposing)
    {
        if (!disposedValue) {
            timer.Stop();
            icon.Visible = false;
            icon.Dispose();
            menu.Dispose();
            disposedValue = true;
        }
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    #endregion
}
