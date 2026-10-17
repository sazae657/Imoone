namespace ImogeIndex.Web6.Ganadores;

/// <summary>
/// ﾄﾚｲｱｲｺﾝ関連
/// </summary>
public sealed class NiraTrayIcon : IDisposable
{
    readonly ITrayBackend backend;
    bool visible;
    bool disposed;
    string text = "";

    public event Action? RestoreRequested;
    public event Action? ExitRequested;
    public event Action? RegistrationLost;

    public NiraTrayIcon(string iconPath, string exitText)
        : this(new Win32TrayBackend(iconPath, exitText)) { }

    public NiraTrayIcon(string iconPath, IReadOnlyList<TrayMenuCommand?> commands)
        : this(new Win32TrayBackend(iconPath, commands)) { }

    public NiraTrayIcon(ITrayBackend backend)
    {
        this.backend = backend;
        backend.Activated += OnActivated;
        backend.ExitSelected += OnExitSelected;
        backend.TaskbarCreated += OnTaskbarCreated;
    }

    void OnActivated() => RestoreRequested?.Invoke();
    void OnExitSelected() => ExitRequested?.Invoke();

    /// <summary>
    /// ﾄﾚｲに表示
    /// </summary>
    /// <returns></returns>
    public bool TryShow()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (visible)
        {
            return true;
        }
        visible = backend.Add(text);
        return visible;
    }

    /// <summary>
    /// ﾄｲﾚから除去
    /// </summary>
    public void Hide()
    {
        if (!visible || disposed)
        {
            return;
        }
        visible = false;
        backend.Delete();
    }

    /// <summary>
    /// 更新
    /// </summary>
    /// <param name="title"></param>
    /// <param name="profile"></param>
    public void UpdateText(string title, string? profile)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        var updatedText = FormatText(title, profile);
        if (text == updatedText)
        {
            return;
        }
        text = updatedText;
        if (visible && !backend.Modify(text))
        {
            Hide();
            RegistrationLost?.Invoke();
        }
    }

    /// <summary>
    /// 通知領域の文字数制限を超えないように丸める
    /// </summary>
    /// <param name="title"></param>
    /// <param name="profile"></param>
    /// <returns></returns>
    internal static string FormatText(string title, string? profile)
    {
        var value = string.IsNullOrEmpty(profile) ? title : title + " - " + profile;
        value = value.Replace('\r', ' ').Replace('\n', ' ');
        if (value.Length > 63)
        {
            var length = char.IsHighSurrogate(value[61]) ? 61 : 62;
            value = value[..length] + "…";
        }
        return value;
    }

    void OnTaskbarCreated()
    {
        if (!visible || disposed)
        {
            return;
        }
        visible = backend.Add(text);
        if (!visible)
        {
            RegistrationLost?.Invoke();
        }
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }
        Hide();
        disposed = true;
        backend.Activated -= OnActivated;
        backend.ExitSelected -= OnExitSelected;
        backend.TaskbarCreated -= OnTaskbarCreated;
        backend.Dispose();
    }
}

/// <summary>
/// ﾄﾚｲｱｲｺﾝのﾊﾝﾄﾞﾗー
/// </summary>
public interface ITrayBackend : IDisposable
{
    event Action? Activated;
    event Action? ExitSelected;
    event Action? TaskbarCreated;
    bool Add(string text);
    bool Modify(string text);
    void Delete();
}
