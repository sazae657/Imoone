using System.Xml.Linq;

namespace ImogeIndex.Common;

/// <summary>
/// ﾄﾞｷｭﾒﾝﾄ用
/// </summary>
public class DocBlock
{
    public string? Title { get; set; }
    public string? Url { get; set; }
    public string? Body { get; set; }

    /// <summary>
    /// XElementに
    /// </summary>
    /// <returns></returns>
    public XElement AsXml()
    {
        return new XElement(nameof(DocBlock),
            new XElement(nameof(Title), Title),
            new XElement(nameof(Url), Url),
            new XElement(nameof(Body), Body)
        );
    }

    /// <summary>
    /// XElementから
    /// </summary>
    /// <param name="e"></param>
    /// <returns></returns>
    public static DocBlock FromXML(XElement e)
    {
        return new DocBlock {
            Title = e.Element(nameof(Title))?.Value,
            Url = e.Element(nameof(Url))?.Value,
            Body = e.Element(nameof(Body))?.Value,
        };
    }
}
