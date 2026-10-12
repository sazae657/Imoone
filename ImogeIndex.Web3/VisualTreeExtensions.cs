using System.Windows;
using System.Windows.Media;

namespace ImogeIndex.Web3;

/// <summary>
/// ﾋﾞｼﾞｭｱﾙﾂﾘー操作
/// </summary>
public static class VisualTreeExtensions
{
    /// <summary>
    /// 自身から親方向に辿って最初に見つかった型Tの要素を返す
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="element"></param>
    /// <returns></returns>
    public static T? FindAncestor<T>(this DependencyObject? element) where T : DependencyObject
    {
        while (element != null)
        {
            if (element is T result)
            {
                return result;
            }
            element = VisualTreeHelper.GetParent(element);
        }
        return null;
    }
}
