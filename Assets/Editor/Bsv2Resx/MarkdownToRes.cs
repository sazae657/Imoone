using ImogeIndex.Common;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Bsv2Resx;

/// <summary>
/// 固定ﾌｫーﾏｯﾄmdからﾘｿーｽ作成
/// </summary>
internal class MarkdownToRes
{
    FileInfo resPath;
    FileInfo mdPath;
    List<FileInfo> insertion = new();
    public MarkdownToRes(FileInfo res, FileInfo md, FileInfo? ins)
    {
        resPath = res;
        mdPath = md;
        if (ins != null) {
            insertion.Add(ins);
        }
    }

    public void Process() {
        if (!mdPath.Exists) {
            throw new FileNotFoundException($"mdが無い => {mdPath.FullName}", mdPath.FullName);
        }
        var mur = Parse(mdPath);
        insertion.Reverse();
        foreach (var n in insertion) {
            mur.Insert(0, ParseInsertion(n));
        }

        var xd = new XDocument(new XElement("DocBlocks", mur.Select(w => w.AsXml())));
        resPath.WriteAndStore(fo => xd.Save(fo));
        Console.WriteLine($"保存: {resPath}");
    }

    enum State
    {
        Title,
        Url,
        BeginDocument,
        Document
    }

    readonly Regex NL = new(@"\r?\n", RegexOptions.Compiled, TimeSpan.FromSeconds(1));

    DocBlock ParseInsertion(FileInfo path)
    {
        var doc = new DocBlock();
        using var far = path.OpenText();
        doc.Body = far.ReadToEnd();
        return doc;
    }

    List<DocBlock> Parse(FileInfo path)
    {
        var stat = State.Title;
        var doc = new DocBlock();
        var docbuf = new StringBuilder();
        var entries = new List<DocBlock>();
        using var far = path.OpenText();
        foreach (var a in NL.Split(far.ReadToEnd()).Select(m => m.TrimEnd())) {
            switch (stat) {
                case State.Title:
                    if (a.StartsWith('#')) {
                        doc = new DocBlock();
                        entries.Add(doc);
                        var pn = a.Substring(1).Trim();
                        doc.Title = pn;
                        stat = State.Url;
                    }
                    break;
                case State.Url:
                    if (a.StartsWith("http")) {
                        var pn = a.Trim();
                        doc.Url = pn;
                        stat = State.BeginDocument;
                    }
                    break;
                case State.BeginDocument:
                    if (a.StartsWith("```")) {
                        stat = State.Document;
                    }
                    break;
                case State.Document:
                    if (a.StartsWith("```")) {
                        stat = State.Title;
                        doc.Body = docbuf.ToString();
                        docbuf.Clear();
                    }
                    else {
                        docbuf.Append(a.TrimEnd()).Append(Environment.NewLine);
                    }
                    break;
                default:
                    throw new NotImplementedException($"{stat} : なにそれこわい");
            }
        }
        if (stat != State.Title) {
            throw new Exception($"{stat} : 終了ステートがおかしい");
        }
        return entries;
    }

}
