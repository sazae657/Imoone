using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.Mvvm.Messaging.Messages;
using ImogeIndex;
using ImogeIndex.Web3.Dialogs;
using ImogeIndexWPF.Settingure.Dictators;
using ImogeIndexWPF.Settingure.Modelle;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace ImogeIndexWPF.Settingure;

public sealed record CloseSettingurewMessage;

/// <summary>
/// 入力中(LostFocus待ち)の値をｿーｽへ確定させる要求
/// 確定できたら true を返す
/// </summary>
public sealed class CommitPendingEditMessage : RequestMessage<bool>;

/// <summary>
/// SettingureWindow.xaml の相互作用ﾛｼﾞｯｸ
/// </summary>
public partial class SettingureWindow : ImogeIndex.Web3.Nuendow
{
    public SettingureWindow(Type? initPage =null)
    {
        DataContext = new SettingureShellModelle(new SettingJailer(this, Profile.Active.Source), initPage);
        Loaded += (_, __) => {
            WeakReferenceMessenger.Default.Register<CloseSettingurewMessage>(this, OnCloseRequested);
            WeakReferenceMessenger.Default.Register<CommitPendingEditMessage>(this, OnCommitPendingEditRequested);
        };
        Unloaded += (_,__) => WeakReferenceMessenger.Default.UnregisterAll(this);
        InitializeComponent();
    }

    class SettingJailer : ISettingsJail {
        readonly Window owner;
        public Settings Settings { get; private set; }

        public SettingJailer(Window owner, Settings s) {
            this.owner = owner;
            Settings = s;
        }

        public async Task<Settings> SaveAsync(Settings settings) {
            // 編集中の設定がそのまま稼働中の設定にならないよう、ｺﾋﾟーを確定させる
            var committed = settings.DeepCopy();
            if (string.IsNullOrEmpty(committed.IPv7)) {
                return committed;
            }
            // 検証を兼ねて先に組み立て、ﾌｧｲﾙ保存に成功してから稼働中の設定を差し替える
            var prepared = committed.Prepare();
            await committed.Jail();
            Profile.Dictator.Settingure[committed.IPv7] = prepared;
            Settings = committed;
            return committed;
        }

        public void ReportSaveFailure(Exception ex) {
            TaskenDialog.Error(owner, ex.Message, Properties.Resources.SW_E_Error, Properties.Resources.SW_E_SaveFailed);
        }
    }

    private void ListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is not ListView view) {
            return;
        }
        if (DataContext is SettingureShellModelle shell &&
            view.SelectedItem is CategoryModelle category) {
            shell.Navigate(category);
            // 遷移を許す代わりに、破棄されたﾍﾟーｼﾞのｴﾗーを数え直す
            Dispatcher.BeginInvoke(RefreshBindingErrors, System.Windows.Threading.DispatcherPriority.Loaded);
        }
    }

    private void OnBindingErrorChanged(object sender, RoutedEventArgs e)
    {
        RefreshBindingErrors();
    }

    /// <summary>
    /// 画面上のﾊﾞｲﾝﾃﾞｨﾝｸﾞｴﾗー有無をｾｯｼｮﾝへ反映する
    /// </summary>
    private bool RefreshBindingErrors()
    {
        var hasError = BindingErrorPoliticalOfficer.HasError(this);
        if (DataContext is SettingureShellModelle shell) {
            shell.Session.HasBindingErrors = hasError;
        }
        return hasError;
    }

    private void OnCloseRequested(object recipient, CloseSettingurewMessage message) {
        Close();
    }

    /// <summary>
    /// ﾌｫーｶｽ中のTextBoxの値をｿーｽへ確定させる
    /// ｷーﾎﾞーﾄﾞ操作やｺﾏﾝﾄﾞ経由で保存された場合はLostFocusが来ないことがあるため
    /// </summary>
    private void OnCommitPendingEditRequested(object recipient, CommitPendingEditMessage message)
    {
        if (Keyboard.FocusedElement is TextBox box) {
            box.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
        }
        // 数値に変換できない等でｿーｽに入らなかった欄が残っていたら保存させない
        message.Reply(!RefreshBindingErrors());
    }

    /// <summary>
    /// ｷー入力の時点で変更ありにする
    /// LostFocusで確定する欄だけ編集した場合でも保存できるようにするため
    /// </summary>
    private void OnTextChanged(object sender, TextChangedEventArgs e)
    {
        // ﾍﾟーｼﾞ表示時のﾊﾞｲﾝﾃﾞｨﾝｸﾞによる流し込みは変更扱いしない
        if (e.OriginalSource is not TextBoxBase box || !box.IsKeyboardFocusWithin) {
            return;
        }
        if (DataContext is SettingureShellModelle shell) {
            shell.Session.MarkDirty();
        }
    }
}
