using ImogeIndex.Web6.Dictators;
using ImogeIndex.Web6.Modelle;
using System.Windows;

namespace ImooneWPF;

// 左ﾍﾟｲﾝのﾌｫﾙﾀﾞーﾂﾘー(本体はFolderTreeCommitteeで、ここはｳｨﾝﾄﾞｳ側の入口だけ)
public partial class MainWindow
{
    /// <summary>
    /// ﾂﾘーの選択変更
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void FolderTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (!sanitized || wasClosing || e.NewValue is not FolderNode node) {
            return;
        }
        if (!folderTree.Select(node)) {
            return;
        }
        if (!string.IsNullOrEmpty(DC.ActiveProfile)) {
            SettingsDictator.Default.LastFolders[DC.ActiveProfile] = folderTree.SelectedLocation;
        }
    }
}
