using System.IO;
using System.Resources;

namespace Bsv2Resx;

/// <summary>
/// resxを編集致す
/// </summary>
internal class ResxEditorize
{
    /// <summary>
    /// 文字列ﾘｿーｽ
    /// </summary>
    public Dictionary<string, string> Resources { get; } = new();

    /// <summary>
    /// 非文字列ﾘｿーｽ
    /// </summary>
    readonly List<ResXDataNode> preservedNodes = new();

    /// <summary>
    /// 既存のｺﾒﾝﾄ
    /// </summary>
    readonly Dictionary<string, string?> comments = new();

    readonly FileInfo resxPath;

    public ResxEditorize(FileInfo path)
    {
        resxPath = path;
        Load(path);
    }

    void Load(FileInfo path)
    {
        if (!path.Exists) {
            return;
        }
        using var reader = new ResXResourceReader(path.FullName) { UseResXDataNodes = true };
        var dict = reader.GetEnumerator();
        while (dict.MoveNext()) {
            var node = (ResXDataNode)dict.Value!;
            var key = node.Name;

            // ﾌｧｲﾙ参照がある場合はそのまま退避
            if (node.FileRef != null) {
                preservedNodes.Add(node);
                continue;
            }

            // これ文字列？
            var obzekt = node.GetValue((System.ComponentModel.Design.ITypeResolutionService?)null);
            if (obzekt is string strVal) {
                Resources[key] = strVal;
                if (!string.IsNullOrEmpty(node.Comment)) {
                    comments[key] = node.Comment;
                }
            }
            else {
                // 埋め込みﾊﾞｲﾅﾘは退避
                preservedNodes.Add(node);
            }
        }
    }

    public void Write(Dictionary<string, string> values)
    {
        using var writer = new ResXResourceWriter(resxPath.FullName);

        // 退避しておいた非文字列ﾘｿーｽをそのまま書き戻す
        foreach (var node in preservedNodes) {
            if (!values.ContainsKey(node.Name)) {
                writer.AddResource(node);
            }
        }

        // 文字列ﾘｿーｽを書き出す
        foreach (var kvp in values) {
            var node = new ResXDataNode(kvp.Key, kvp.Value);
            // 既存のｺﾒﾝﾄがあれば引き継ぐ
            if (comments.TryGetValue(kvp.Key, out var comment) && !string.IsNullOrEmpty(comment)) {
                node.Comment = comment;
            }
            writer.AddResource(node);
        }

        writer.Generate();
    }
}
