using System.Collections;
using System.Windows.Controls;

namespace ImooneWPF.Controls;

/// <summary>
/// ﾒｲﾝの色々ﾋﾞｭー
/// </summary>
public sealed class EssenUndFressenView : ListView
{
    /// <summary>
    /// 範囲選択をまとめて詳細ﾍﾟｲﾝ更新ﾗｯｼｭを止める
    /// </summary>
    /// <param name="items"></param>
    public void SetSelection(IEnumerable items) => SetSelectedItems(items);
}
