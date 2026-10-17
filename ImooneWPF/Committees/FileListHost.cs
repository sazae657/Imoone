using ImogeIndex.Web6.Committees;
using ImogeIndex.Mur;
using System.Windows.Controls;

namespace ImooneWPF.Committees;

/// <summary>
/// 検索結果の流し込み先
/// </summary>
/// <param name="list">一覧</param>
/// <param name="searchBox">検索ﾎﾞｯｸｽ</param>
/// <param name="columns">並び順と並べ替え表示</param>
internal sealed class FileListHost(ListView list, TextBox searchBox, FileListColumnCommittee columns) : IFileListHost
{
    public string QueryText => searchBox.Text;

    public bool HasItemsSource => list.ItemsSource != null;

    public FileItem? SelectedItem => list.SelectedItem as FileItem;

    public void Publish(IReadOnlyList<FileItem> items) => list.ItemsSource = items;

    public void Reveal(FileItem item)
    {
        list.SelectedItem = item;
        list.ScrollIntoView(item);
    }

    public IComparer<FileItem>? CreateComparer() => columns.CreateComparer();

    public void SetSortIndicatorVisible(bool visible) => columns.SetSortIndicatorVisible(visible);
}
