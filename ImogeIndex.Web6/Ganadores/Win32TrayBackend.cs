using System.ComponentModel;
using System.Runtime.InteropServices;

namespace ImogeIndex.Web6.Ganadores;

/// <summary>
/// ﾄﾚｲｱｲｺﾝ用隠しｳｨﾝﾄﾞｳ
/// </summary>
internal sealed class Win32TrayBackend : ITrayBackend
{
    const uint CallbackMessage = 0x8001;
    readonly WindowProc procedure;
    readonly string className = "ImogeIndex.Web6." + Guid.NewGuid().ToString("N");
    readonly IReadOnlyList<TrayMenuCommand?> commands;
    readonly nint instance = NativeMethods.GetModuleHandleW(null);
    readonly uint taskbarMessage;
    nint window;
    nint icon;
    ushort atom;
    bool ownsIcon;
    bool disposed;

    public event Action? Activated;
    public event Action? ExitSelected;
    public event Action? TaskbarCreated;

    internal Win32TrayBackend(string iconPath, string exitText)
        : this(iconPath, Array.Empty<TrayMenuCommand?>())
    {
        commands = [new TrayMenuCommand(exitText, () => ExitSelected?.Invoke())];
    }

    internal Win32TrayBackend(string iconPath, IReadOnlyList<TrayMenuCommand?> commands)
    {
        this.commands = commands;
        procedure = ProcessMessage;
        try
        {
            // Explorer再起動系のやつ拾う
            taskbarMessage = NativeMethods.RegisterWindowMessageW("TaskbarCreated");
            if (taskbarMessage == 0)
            {
                throw new Win32Exception();
            }
            var windowClass = new WNDCLASSEXW {
                Size = (uint)Marshal.SizeOf<WNDCLASSEXW>(),
                Procedure = Marshal.GetFunctionPointerForDelegate(procedure),
                Instance = instance,
                ClassName = className
            };
            atom = NativeMethods.RegisterClassExW(ref windowClass);
            if (atom == 0)
            {
                throw new Win32Exception();
            }
            // HWND_MESSAGEではExplorerからのﾌﾞﾛーﾄﾞｷｬｽﾄを受信できないのでｳｲﾝﾄﾞｳ作る
            window = NativeMethods.CreateWindowExW(0, className, "", 0, 0, 0, 0, 0, 0, 0, instance, 0);
            if (window == 0)
            {
                throw new Win32Exception();
            }
            icon = NativeMethods.LoadImageW(0, iconPath, 1, NativeMethods.GetSystemMetrics(49), NativeMethods.GetSystemMetrics(50), 0x10);
            ownsIcon = icon != 0;
            if (icon == 0)
            {
                icon = NativeMethods.LoadIconW(0, 32512);
            }
            if (icon == 0)
            {
                throw new Win32Exception();
            }
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    NOTIFYICONDATAW Data(string text = "") => new() {
        Size = (uint)Marshal.SizeOf<NOTIFYICONDATAW>(), Window = window, Id = 1,
        Flags = 1 | 2 | 4 | 0x80, Callback = CallbackMessage, Icon = icon,
        Tip = text, Info = "", InfoTitle = ""
    };

    public bool Add(string text)
    {
        var data = Data(text);
        if (!NativeMethods.Shell_NotifyIconW(0, ref data))
        {
            return false;
        }
        data.Version = 4;
        if (!NativeMethods.Shell_NotifyIconW(4, ref data))
        {
            Delete();
            return false;
        }
        return true;
    }

    public bool Modify(string text)
    {
        var data = Data(text);
        return NativeMethods.Shell_NotifyIconW(1, ref data);
    }

    public void Delete()
    {
        var data = Data();
        NativeMethods.Shell_NotifyIconW(2, ref data);
    }

    nint ProcessMessage(nint hwnd, uint message, nuint wParam, nint lParam)
    {
        try
        {
            if (!disposed && message == taskbarMessage)
            {
                TaskbarCreated?.Invoke();
                return 0;
            }
            if (!disposed && message == CallbackMessage)
            {
                var notification = (uint)((long)lParam & 0xffff);
                if (notification is 0x400 or 0x401)
                {
                    Activated?.Invoke();
                }
                else if (notification == 0x7b)
                {
                    ShowMenu(hwnd, wParam);
                }
                return 0;
            }
        }
        catch (Exception ex)
        {
            // ﾈｲﾃｨﾌﾞのｳｨﾝﾄﾞｳﾌﾟﾛｼーｼﾞｬに流れると酷いことになるので握りつぶす
            System.Diagnostics.Trace.TraceError(ex.ToString());
        }
        return NativeMethods.DefWindowProcW(hwnd, message, wParam, lParam);
    }

    void ShowMenu(nint hwnd, nuint position)
    {
        var menu = NativeMethods.CreatePopupMenu();
        if (menu == 0)
        {
            return;
        }
        uint command = 0;
        try
        {
            for (var i = 0; i < commands.Count; i++)
            {
                var item = commands[i];
                var flags = item == null ? 0x800u : item.IsEnabled ? 0u : 1u;
                if (!NativeMethods.AppendMenuW(menu, flags, (nuint)(i + 1), item?.Text ?? ""))
                {
                    return;
                }
            }
            var x = unchecked((short)(position & 0xffff));
            var y = unchecked((short)((position >> 16) & 0xffff));
            NativeMethods.SetForegroundWindow(hwnd);
            command = NativeMethods.TrackPopupMenu(menu, 0x100 | 2, x, y, 0, hwnd, 0);
            NativeMethods.PostMessageW(hwnd, 0, 0, 0);
            var data = Data();
            NativeMethods.Shell_NotifyIconW(3, ref data);
        }
        finally
        {
            NativeMethods.DestroyMenu(menu);
        }
        if (command > 0 && command <= commands.Count)
        {
            commands[(int)command - 1]?.Invoke();
        }
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }
        disposed = true;
        if (window != 0)
        {
            NativeMethods.DestroyWindow(window);
            window = 0;
        }
        if (ownsIcon)
        {
            NativeMethods.DestroyIcon(icon);
            ownsIcon = false;
        }
        if (atom != 0)
        {
            NativeMethods.UnregisterClassW(className, instance);
            atom = 0;
        }
        GC.KeepAlive(procedure);
    }

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    delegate nint WindowProc(nint hwnd, uint message, nuint wParam, nint lParam);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct WNDCLASSEXW
    {
        internal uint Size, Style;
        internal nint Procedure;
        internal int ClassExtra, WindowExtra;
        internal nint Instance, Icon, Cursor, Background;
        internal string? MenuName;
        internal string ClassName;
        internal nint SmallIcon;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct NOTIFYICONDATAW
    {
        internal uint Size;
        internal nint Window;
        internal uint Id, Flags, Callback;
        internal nint Icon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        internal string Tip;
        internal uint State, StateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        internal string Info;
        internal uint Version;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        internal string InfoTitle;
        internal uint InfoFlags;
        internal Guid Guid;
        internal nint BalloonIcon;
    }

    /// <summary>
    /// ﾈーﾁﾌﾞ
    /// </summary>
    static class NativeMethods
    {
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        public static extern nint GetModuleHandleW(string? name);
        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern ushort RegisterClassExW(ref WNDCLASSEXW value);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern bool UnregisterClassW(string name, nint instance);
        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern nint CreateWindowExW(uint extendedStyle, string className, string title, uint style,
            int x, int y, int width, int height, nint parent, nint menu, nint instance, nint parameter);
        [DllImport("user32.dll")]
        public static extern bool DestroyWindow(nint hwnd);
        [DllImport("user32.dll")]
        public static extern nint DefWindowProcW(nint hwnd, uint message, nuint wParam, nint lParam);
        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern uint RegisterWindowMessageW(string name);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern nint LoadImageW(nint instance, string name, uint type, int width, int height, uint flags);
        [DllImport("user32.dll")]
        public static extern nint LoadIconW(nint instance, nint name);
        [DllImport("user32.dll")]
        public static extern bool DestroyIcon(nint icon);
        [DllImport("user32.dll")]
        public static extern int GetSystemMetrics(int index);
        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        public static extern bool Shell_NotifyIconW(uint operation, ref NOTIFYICONDATAW data);
        [DllImport("user32.dll")]
        public static extern nint CreatePopupMenu();
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern bool AppendMenuW(nint menu, uint flags, nuint id, string text);
        [DllImport("user32.dll")]
        public static extern uint TrackPopupMenu(nint menu, uint flags, int x, int y, int reserved, nint hwnd, nint rectangle);
        [DllImport("user32.dll")]
        public static extern bool DestroyMenu(nint menu);
        [DllImport("user32.dll")]
        public static extern bool SetForegroundWindow(nint hwnd);
        [DllImport("user32.dll")]
        public static extern bool PostMessageW(nint hwnd, uint message, nuint wParam, nint lParam);
    }

}
