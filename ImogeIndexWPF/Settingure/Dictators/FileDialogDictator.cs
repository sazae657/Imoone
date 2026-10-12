using ImogeIndexWPF.Dictators;
using Microsoft.Win32;

namespace ImogeIndexWPF.Settingure.Dictators;

internal class FileDialogDictator : IFileDialogDictator
{
    public string? OpenFileDialog(string title = "Open", string filter = "All files (*.*)|*.*", string initialDirectory = "")
    {
        var dialog = new OpenFileDialog {
            Title = title,
            InitialDirectory = initialDirectory,
            Filter = filter, //"ﾃｷｽﾄﾌｧｲﾙ (*.txt)|*.txt|すべてのﾌｧｲﾙ (*.*)|*.*"
            CheckFileExists = true,
            Multiselect = false
        };

        // ﾀﾞｲｱﾛｸﾞを表示し、OKが押されたか判定
        if (dialog.ShowDialog() == true) {
            return dialog.FileName;
        }

        return null;
    }
}
