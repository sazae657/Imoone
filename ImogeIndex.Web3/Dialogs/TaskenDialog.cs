using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Interop;

namespace ImogeIndex.Web3.Dialogs;


/// <summary>
/// ﾎﾞﾀﾑ
/// </summary>
[Flags]
public enum TaskDialogCommonButton {
    Ok = 0x0001,
    Yes = 0x0002,
    No = 0x0004,
    Cancel = 0x0008,
    Retry = 0x0010,
    Close = 0x0020
}

/// <summary>
/// ｱｲｺﾝ
/// </summary>
public static class TaskDialogIcon {
    /// <summary>
    /// TD_WARNING_ICON
    /// </summary>
    public static readonly IntPtr Warning = new IntPtr(0xFFFF & 65535);

    /// <summary>
    /// TD_ERROR_ICON
    /// </summary>
    public static readonly IntPtr Error = new IntPtr(0xFFFF & 65534);

    /// <summary>
    /// TD_INFORMATION_ICON
    /// </summary>
    public static readonly IntPtr Information = new IntPtr(0xFFFF & 65533);

    /// <summary>
    /// TD_SHIELD_ICON
    /// </summary>
    public static readonly IntPtr Shield = new IntPtr(0xFFFF & 65532); 
}

/// <summary>
/// 押されたﾎﾞﾀﾑ
/// </summary>
public enum TaskDialogResult {
    Ok = 1,
    Cancel = 2,
    Retry = 4,
    Yes = 6,
    No = 7,
    Close = 8
}

/// <summary>
/// TaskDialogﾍﾙﾊﾟー
/// </summary>
public class TaskenDialog {
    static class NativeMethods {
        [DllImport("comctl32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern int TaskDialog(
            IntPtr hwndOwner,
            IntPtr hInstance,
            string pszWindowTitle,
            string pszMainInstruction,
            string pszContent,
            TaskDialogCommonButton dwCommonButtons,
            IntPtr pszIcon,
            out int pnButton);
    }

    /// <summary>
    /// Infoｱｲｺﾝ付き
    /// </summary>
    /// <param name="owner"></param>
    /// <param name="message"></param>
    /// <param name="title"></param>
    /// <param name="instruction"></param>
    /// <returns></returns>
    public static TaskDialogResult Info(Window owner, string message, string title = "", string instruction = "") {
        IntPtr hwnd = new WindowInteropHelper(owner).Handle;

        int resultButton;
        int hr = NativeMethods.TaskDialog(
            hwndOwner: hwnd,
            hInstance: IntPtr.Zero,
            pszWindowTitle: title,
            pszMainInstruction: instruction,
            pszContent: message,
            dwCommonButtons: TaskDialogCommonButton.Ok,
            pszIcon: TaskDialogIcon.Information,
            pnButton: out resultButton);
        if (hr != 0) {
            MessageBox.Show(owner, $"{instruction}\n{message}", title, MessageBoxButton.OK);
            return TaskDialogResult.Ok;
        }
        return (TaskDialogResult)resultButton;
    }

    /// <summary>
    /// 警告ｱｲｺﾝ付き
    /// </summary>
    /// <param name="owner"></param>
    /// <param name="message"></param>
    /// <param name="title"></param>
    /// <param name="instruction"></param>
    /// <returns></returns>
    public static TaskDialogResult Warning(Window owner, string message, string title = "", string instruction = "") {
        IntPtr hwnd = new WindowInteropHelper(owner).Handle;

        int resultButton;
        int hr = NativeMethods.TaskDialog(
            hwndOwner: hwnd,
            hInstance: IntPtr.Zero,
            pszWindowTitle: title,
            pszMainInstruction: instruction,
            pszContent: message,
            dwCommonButtons: TaskDialogCommonButton.Ok,
            pszIcon: TaskDialogIcon.Warning,
            pnButton: out resultButton);
        if (hr != 0) {
            MessageBox.Show(owner, $"{instruction}\n{message}", title, MessageBoxButton.OK, MessageBoxImage.Warning);
            return TaskDialogResult.Ok;
        }
        return (TaskDialogResult)resultButton;
    }

    /// <summary>
    /// ｴﾗーｱｲｺﾝ付き
    /// </summary>
    /// <param name="owner"></param>
    /// <param name="message"></param>
    /// <param name="title"></param>
    /// <param name="instruction"></param>
    /// <returns></returns>
    public static TaskDialogResult Error(Window owner, string message, string title = "", string instruction = "") {
        IntPtr hwnd = new WindowInteropHelper(owner).Handle;

        int resultButton;
        int hr = NativeMethods.TaskDialog(
            hwndOwner: hwnd,
            hInstance: IntPtr.Zero,
            pszWindowTitle: title,
            pszMainInstruction: instruction,
            pszContent: message,
            dwCommonButtons: TaskDialogCommonButton.Ok,
            pszIcon: TaskDialogIcon.Error,
            pnButton: out resultButton);
        if (hr != 0) {
            MessageBox.Show(owner, $"{instruction}\n{message}", title, MessageBoxButton.OK, MessageBoxImage.Error);
            return TaskDialogResult.Ok;
        }
        return (TaskDialogResult)resultButton;
    }
}
