using CommunityToolkit.Mvvm.Messaging;
using ImogeIndex.Web3.Dialogs;
using ImogeIndexWPF.Modelle;
using ImogeIndexWPF.Settingure;
using ImogeIndexWPF.Tools.Ansicht;
using System.Windows;

namespace ImogeIndexWPF.Dictators;

/// <summary>
/// ﾒｲﾝｳｲﾝﾄﾞｳから開く子ｳｲﾝﾄﾞｳ統治
/// </summary>
internal sealed class SubWindowDictator
{
    readonly Window owner;
    SettingureWindow? settingure;
    RegexSpielplatzWindow? regexSandbox;
    AboutWeb3Dialog? aboutDialog;

    public SubWindowDictator(Window owner)
    {
        this.owner = owner;
    }

    /// <summary>
    /// 設定ｳｲﾝﾄﾞｳ開いてる？
    /// </summary>
    public bool IsSettingureOpen => settingure != null;

    /// <summary>
    /// 別窓を開くﾒｯｾーｼﾞを受け取るようにする
    /// </summary>
    public void RegisterMessages()
    {
        WeakReferenceMessenger.Default.Register<OpenToolMessage>(this, OnOpenToolMassage);
        WeakReferenceMessenger.Default.Register<OpenRegexSandboxMessage>(this, OnOpenRegexSandboxMassage);
    }

    /// <summary>
    /// 設定開く
    /// </summary>
    /// <param name="page">最初に開くﾍﾟーｼﾞ</param>
    public void OpenSettingure(Type? page)
    {
        if (settingure != null) {
            settingure.Focus();
            return;
        }
        var window = new SettingureWindow(page);
        ShowSized(window, WindowParamsKey.Settingure, () => settingure = null);
        settingure = window;
    }

    /// <summary>
    /// 設定ｳｲﾝﾄﾞｳを手前に出す
    /// </summary>
    public void ActivateSettingure()
    {
        settingure?.Activate();
    }

    /// <summary>
    /// 設定ｳｲﾝﾄﾞｳを閉じる
    /// </summary>
    public void CloseSettingure()
    {
        settingure?.Close();
        settingure = null;
    }

    /// <summary>
    /// 正規表現練習帳開く
    /// </summary>
    /// <param name="regex">入れておく正規表現</param>
    public void OpenRegexSandbox(string? regex)
    {
        if (regexSandbox != null) {
            if (!string.IsNullOrEmpty(regex)) {
                regexSandbox.SetRegex(regex);
            }
            regexSandbox.Focus();
            return;
        }
        var window = new RegexSpielplatzWindow();
        ShowSized(window, WindowParamsKey.RegexSpielplatz, () => regexSandbox = null);
        regexSandbox = window;
        if (!string.IsNullOrEmpty(regex)) {
            window.SetRegex(regex);
        }
    }

    /// <summary>
    /// ｱﾊﾞｳﾂを開く
    /// </summary>
    public void OpenAboutDialog()
    {
        if (aboutDialog != null) {
            aboutDialog.Focus();
            return;
        }
        var dialog = new AboutWeb3Dialog {
            Owner = owner
        };
        dialog.Closed += (_, _) => aboutDialog = null;
        dialog.SetClientName(Properties.Resources.ML_AppTitle);
        aboutDialog = dialog;
        dialog.Show();
    }

    /// <summary>
    /// 全部閉じる(終了時用)
    /// </summary>
    public void CloseAll()
    {
        ClosingStep.Run("Settingure", () => settingure?.Close());
        settingure = null;
        ClosingStep.Run("RegexSandbox", () => regexSandbox?.Close());
        regexSandbox = null;
        ClosingStep.Run("AboutDialog", () => aboutDialog?.Close());
        aboutDialog = null;
    }

    /// <summary>
    /// 前回のｻｲｽﾞで開き、閉じたらｻｲｽﾞを書き戻す
    /// </summary>
    /// <param name="window"></param>
    /// <param name="key"></param>
    /// <param name="onClosed">閉じた後の後始末</param>
    void ShowSized(Window window, WindowParamsKey key, Action onClosed)
    {
        var swp = WarpSettings.Default.GetWindowParams(key);
        window.Owner = owner;
        window.Width = swp.Width;
        window.Height = swp.Height;
        window.Closed += (_, _) => {
            WarpSettings.Default.WindowParamOf(key, param => {
                param.Width = (int)window.Width;
                param.Height = (int)window.Height;
            });
            onClosed();
        };
        window.Show();
    }

    /// <summary>
    /// 別窓開いてﾒｯｾーｼﾞ
    /// </summary>
    /// <param name="o"></param>
    /// <param name="msg"></param>
    void OnOpenToolMassage(object o, OpenToolMessage msg)
    {
        switch (msg.Type) {
            case OpenToolType.Settingure:
                OpenSettingure(null);
                break;
            case OpenToolType.RegexSandbox:
                OpenRegexSandbox(null);
                break;
            case OpenToolType.AboutDialog:
                OpenAboutDialog();
                break;
            default:
                break;
        }
    }

    /// <summary>
    /// 正規表現練習帳開いてﾒｯｾーｼﾞ
    /// </summary>
    /// <param name="o"></param>
    /// <param name="msg"></param>
    void OnOpenRegexSandboxMassage(object o, OpenRegexSandboxMessage msg)
    {
        OpenRegexSandbox(msg.Regex);
    }
}
