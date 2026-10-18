using ImogeIndex;
using ImogeIndex.Mur;
using ImogeIndex.Web6.Dictators;
using System.Windows.Controls;

namespace ImooneWPF;

// ﾕーｻﾞー定義ﾗﾍﾞﾙの付け外し
public partial class MainWindow
{
    /// <summary>
    /// 右ｸﾘﾒﾆｭーのﾗﾍﾞﾙ項目の目印
    /// </summary>
    public const string LabelMenuTag = "LabelMenu";

    readonly LabelDictator labelDictator = new();

    /// <summary>
    /// ﾗﾍﾞﾙを付け外しする対象
    /// 右ｸﾘした項目が選択中なら選択中の全部、そうでなければその項目だけ
    /// </summary>
    /// <param name="clicked"></param>
    /// <returns></returns>
    private List<FileItem> LabelTargets(FileItem clicked)
    {
        var selected = FileListView.SelectedItems.OfType<FileItem>().ToList();
        return selected.Contains(clicked) ? selected : [clicked];
    }

    /// <summary>
    /// 右ｸﾘﾒﾆｭーのﾗﾍﾞﾙ項目を組み立てる
    /// 定義の変更と対象ごとのﾁｪｯｸ状態を反映するので開く度に作り直す
    /// </summary>
    /// <param name="menu"></param>
    /// <param name="clicked"></param>
    private void BuildLabelMenu(ContextMenu menu, FileItem clicked)
    {
        var root = menu.Items.OfType<MenuItem>().FirstOrDefault(x => Equals(x.Tag, LabelMenuTag));
        if (root == null) {
            return;
        }
        root.Items.Clear();
        var targets = LabelTargets(clicked);

        // 一番上は編集画面で固定
        var edit = new MenuItem { Header = Properties.Resources.MC_OpenLabelEdit };
        edit.Click += (_, _) => OnOpenLabelEdit();
        root.Items.Add(edit);

        var defined = (Profile.Active.Source.UserDefinitions?.Labels ?? [])
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var attached = targets.Select(x => x.Label).Distinct(StringComparer.Ordinal).ToList();
        // 全部同じﾗﾍﾞﾙの時だけﾁｪｯｸを付ける
        var common = attached.Count == 1 ? attached[0] : null;
        // 定義から消されても画像には付けたままなので、外せるように出しておく
        var orphans = attached.OfType<string>()
            .Where(x => !defined.Contains(x, StringComparer.Ordinal))
            .Order(StringComparer.Ordinal)
            .ToList();

        if (defined.Count + orphans.Count != 0) {
            root.Items.Add(new Separator());
        }
        foreach (var label in defined.Concat(orphans)) {
            var item = new MenuItem {
                // _ がｱｸｾｽｷー扱いされるのでｴｽｹーﾌﾟ
                Header = label.Replace("_", "__"),
                IsChecked = string.Equals(label, common, StringComparison.Ordinal),
            };
            item.Click += async (_, _) => await ApplyLabelAsync(targets, label);
            root.Items.Add(item);
        }

        root.Items.Add(new Separator());
        var remove = new MenuItem {
            Header = Properties.Resources.MC_RemoveLabel,
            IsEnabled = attached.Any(x => x != null),
        };
        remove.Click += async (_, _) => await ApplyLabelAsync(targets, null);
        root.Items.Add(remove);
    }

    /// <summary>
    /// ﾗﾍﾞﾙの付け外し
    /// </summary>
    /// <param name="targets"></param>
    /// <param name="label">nullなら外す</param>
    /// <returns></returns>
    private async Task ApplyLabelAsync(IReadOnlyList<FileItem> targets, string? label)
    {
        if (wasClosing || wasLoadingProfile) {
            return;
        }
        try {
            var changed = await labelDictator.ApplyAsync(targets, label, profileLoader.Datum.Items, tokenSource.Token);
            if (changed.Count == 0) {
                return;
            }
            fileListSearch.NotifyLabelsChanged();
            // 詳細ﾍﾟｲﾝに出してる項目が書き換わったら出し直す
            if (DC.DetailFields.Count != 0 && FileListView.SelectedItem is FileItem shown && changed.Contains(shown)) {
                detailPane.Show(shown);
            }
        }
        catch (OperationCanceledException) when (wasClosing) {
            // 閉じられた
        }
        catch (Exception ex) {
            ShowErrorDialog(ex);
        }
    }
}
