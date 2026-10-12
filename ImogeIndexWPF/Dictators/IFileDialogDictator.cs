namespace ImogeIndexWPF.Dictators;

/// <summary>
/// ﾌｧｲﾙ開くﾀﾞｲｱﾛｸﾞ
/// </summary>
internal interface IFileDialogDictator
{
    string? OpenFileDialog(string title, string filter = "All files (*.*)|*.*", string initialDirectory = "");
}
