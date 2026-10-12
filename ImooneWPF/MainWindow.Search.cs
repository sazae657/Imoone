using ImogeIndex.Mur;
using System.Windows;

namespace ImooneWPF;

// 検索関連(本体はFileListSearchCommittee)

public partial class MainWindow
{
    /// <summary>
    /// 戻るﾎﾞﾀﾑｸﾘｯｸ
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void ClearSimilarSearch_Click(object sender, RoutedEventArgs e)
    {
        if (wasLoadingProfile || wasClosing) {
            return;
        }
        fileListSearch.Search();
    }

    /// <summary>
    /// 類似画像検索
    /// </summary>
    /// <param name="target"></param>
    private void SearchSimilarImages(FileItem target)
    {
        if (wasLoadingProfile || wasClosing) {
            return;
        }
        fileListSearch.SearchSimilar(target);
    }

    /// <summary>
    /// 使うﾊｯｼｭが変わったら類似検索中なら同じ基準画像で検索し直す
    /// </summary>
    /// <param name="hash"></param>
    private void SimilarHash_Switched(SimilarHashAlgorithm hash)
    {
        if (fileListSearch.SimilarTarget is { } target) {
            SearchSimilarImages(target);
        }
    }
}
