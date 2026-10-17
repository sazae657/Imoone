using ImogeIndex.Mur;
using ImogeIndex.Web6.Storage;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ImooneWPF;

// 詳細ﾍﾟｲﾝとﾌﾟﾚﾋﾞｭー(本体はDetailPaneCommitteeで、ここはｳｨﾝﾄﾞｳ側の入口だけ)
public partial class MainWindow
{
    private async void ToolTip_Opening(object sender, ToolTipEventArgs e)
    {
        if (sender is not ListViewItem item || item.DataContext is not FileItem fileItem)
        {
            return;
        }
        if (item.ToolTip is not ToolTip { Content: StackPanel panel })
        {
            return;
        }
        var image = panel.Children.OfType<Image>().FirstOrDefault();
        if (image == null)
        {
            return;
        }
        await detailPane.FillThumbnailAsync(fileItem, bitmap => image.Source = bitmap,
            () => ReferenceEquals(item.DataContext, fileItem));
    }

    /// <summary>
    /// ｺﾋﾟｨ
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void DetailFieldCopyCommand_Executed(object sender, ExecutedRoutedEventArgs e)
    {
        var field = e.Parameter as MetadataPair ??
            DetailFieldsListView.SelectedItem as MetadataPair;
        if (field == null) {
            return;
        }

        Clipboard.SetText($"{field.Name}|{field.Value}");
    }
}
