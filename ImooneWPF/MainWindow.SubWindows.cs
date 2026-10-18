using ImogeIndex;
using ImogeIndex.Common;
using ImogeIndex.Mur;
using ImogeIndex.Web3.Dialogs;
using ImogeIndex.Web6.Committees;
using ImogeIndex.Web6.Dictators;
using System.Diagnostics;
using System.IO;
using System.Windows;

namespace ImooneWPF;

// ｻﾌﾞｳｨﾝﾄﾞｳと外部起動
public partial class MainWindow
{
    readonly Launcher fileLauncher = new();
    ImogeIndex.Web3.Dialogs.AboutWeb3Dialog? aboutWeb3Dialog;

    NiractionWindow? niractionWindow = null;

    private void OpenFolderCommand_Auction(FileItem obj)
    {
        ShowLaunchResult(fileLauncher.ShowInExplorer(obj.FullName), obj.FullName);
    }

    private void OpenFileCommand_Auction(FileItem obj)
    {
        ShowLaunchResult(fileLauncher.Open(obj.FullName), obj.FullName);
    }

    private void OpenSettingFolder_Auction(FileInfo? path)
    {
        if (path == null) {
            return;
        }
        ShowLaunchResult(fileLauncher.ShowInExplorer(path.FullName), path.FullName);
    }

    /// <summary>
    /// ｱﾊﾞｳﾂ
    /// </summary>
    private void OnOpenAboutDialog()
    {
        if (aboutWeb3Dialog != null) {
            aboutWeb3Dialog.Focus();
            return;
        }
        aboutWeb3Dialog = new();
        aboutWeb3Dialog.Closed += (_, _) => {
            aboutWeb3Dialog = null;
        };
        aboutWeb3Dialog.Owner = this;
        aboutWeb3Dialog.SetClientName(Properties.Resources.MC_AppTitle);
        aboutWeb3Dialog.Show();
    }

    LabelEditWindow? labelEditWindow;

    /// <summary>
    /// ﾗﾍﾞﾙ編集
    /// </summary>
    private void OnOpenLabelEdit()
    {
        if (labelEditWindow != null) {
            labelEditWindow.Focus();
            return;
        }
        labelEditWindow = new(Profile.Active.Source);
        ConnfigureSubwindow(labelEditWindow, WindowParamsKey.LabelEdit);
        labelEditWindow.Closed += (_, _) => StoreSubwindow(labelEditWindow, WindowParamsKey.LabelEdit, _ => labelEditWindow = null);
        labelEditWindow.Owner = this;
        labelEditWindow.Show();
    }

    /// <summary>
    /// ﾆﾗｸｼｮﾝ受信
    /// </summary>
    /// <param name="obj"></param>
    private void OnNiractionResponse(ImogeIndex.Niraction.NiractionResponse? nir, string? error)
    {
        if (wasClosing) {
            return;
        }
        if (nir == null) {
            if (error == null) {
                return;
            }
            TaskenDialog.Error(this, error, Properties.Resources.MC_N_Niraction, Properties.Resources.MC_N_NiractionFailed);
            return;
        }
        if (niractionWindow != null) {
            niractionWindow.Niraction(nir);
            niractionWindow.Focus();
            return;
        }
        niractionWindow = new();
        ConnfigureSubwindow(niractionWindow, WindowParamsKey.Niraction);
        niractionWindow.Closed += (_, _) => StoreSubwindow(niractionWindow, WindowParamsKey.Niraction, _ => niractionWindow = null);
        niractionWindow.Niraction(nir);
        niractionWindow.Show();
    }

    /// <summary>
    /// 小窓保存
    /// </summary>
    /// <param name="w"></param>
    /// <param name="key"></param>
    private void StoreSubwindow(Window w, WindowParamsKey key, Action<Window> delegaty)
    {
        SettingsDictator.Default.WindowParamOf(key, param => {
            param.Width = Math.Max(128, (int)w.Width);
            param.Height = Math.Max(128, (int)w.Height);
        });
        delegaty(w);
    }

    /// <summary>
    /// 小窓復元
    /// </summary>
    /// <param name="w"></param>
    /// <param name="key"></param>
    private void ConnfigureSubwindow(Window w, WindowParamsKey key)
    {
        SettingsDictator.Default.WindowParamOf(key, param => {
            w.Width = Math.Max(128, param.Width);
            w.Height = Math.Max(128, param.Height);
        });
    }

    /// <summary>
    /// ｲﾓｹﾞｲﾝﾃﾞｯｸｽ起動
    /// </summary>
    private void OnLaunchIndexTool() {
        var iid = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ImogeIndexWPF.exe");
        var app = new ProcessStartInfo {
            FileName = iid
        };
        app.ArgumentList.Add("--profile");
        app.ArgumentList.Add(Profile.Dictator.ActiveProfile);
        try {
            Process.Start(app);
        }
        catch (Exception r) {
            ShowErrorDialog(Properties.Resources.MC_E_IndexToolLaunchFailed.Morfat(r.Message));
        }
    }

    /// <summary>
    /// ﾌｧｲﾙ開いた結果
    /// </summary>
    /// <param name="result"></param>
    /// <param name="path"></param>
    private void ShowLaunchResult(FileLaunchResult result, string path)
    {
        switch (result.Status)
        {
            case LaunchStatus.Denied:
                ShowWarningDialog(Properties.Resources.MC_E_ShellExecDenied.Morfat(path));
                break;
            case LaunchStatus.FileNotFound:
                ShowErrorDialog(Properties.Resources.MC_E_FileNotFound.Morfat(path));
                break;
            case LaunchStatus.Failed:
                ShowErrorDialog(Properties.Resources.MC_ErrorError.Morfat(
                    result.Exception?.Message,
                    result.Exception?.StackTrace));
                break;
        }
    }

}
