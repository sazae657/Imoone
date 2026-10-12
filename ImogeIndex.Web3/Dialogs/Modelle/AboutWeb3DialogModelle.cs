using CommunityToolkit.Mvvm.ComponentModel;
using ImogeIndex.Common;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reflection;
using System.Text;
using System.Xml.Linq;

namespace ImogeIndex.Web3.Dialogs.Modelle;

internal class DocumentHolder {
    public string Label { get; set; }
    public DocBlock Doc { get; set; }

    public DocumentHolder(DocBlock doc)
    {
        if (doc.Title == null) {
            throw new ArgumentNullException(Properties.Resources.E_TitleIsNull);
        }
        Label = doc.Title;
        Doc = doc;
    }
}

internal partial class AboutWeb3DialogModelle : ObservableObject
{
    public AboutWeb3DialogModelle() : base()
    {
        Build();
        SelectedDoc = Documents.FirstOrDefault();
        RepositoryUrl = new Uri(ImogeIndex.Properties.EmbedContents.RepositoryUrl);
        var piz = Assembly.GetExecutingAssembly().GetName().Version;
        if (piz != null) {
            Version = piz.ToString();
        }
    }

    public ObservableCollection<DocumentHolder> Documents { get; set; } = new();

    [ObservableProperty]
    public partial DocumentHolder? SelectedDoc { get; set; }

    [ObservableProperty]
    public partial string WindowTitle { get; set; } = ImogeIndex.Properties.Resources.LC_ImogeIndex;

    [ObservableProperty]
    public partial string? Version {get;set; }

    [ObservableProperty]
    public partial string ClientName { get; set; } = ImogeIndex.Properties.Resources.LC_ImogeIndex;

    [ObservableProperty]
    public partial Uri RepositoryUrl { get; set; }

    void Build()
    {
        try {
            var gtr = XElement.Parse(
                ImogeIndex.Properties.EmbedContents.Licenses)?.Elements(nameof(DocBlock));
            if (gtr == null) {
                return;
            }
            Documents = new ObservableCollection<DocumentHolder>(
                gtr.Select(w => new DocumentHolder(DocBlock.FromXML(w))));

            var fd = Documents.FirstOrDefault();
            if (fd != null && (fd.Doc.Body?.Contains("sazae657") ?? false)) {
                fd.Label =
                fd.Doc.Title = ImogeIndex.Properties.Resources.LC_ImogeIndex;
                fd.Doc.Url = ImogeIndex.Properties.EmbedContents.RepositoryUrl;
            }
        }
        catch {
            // むり
        }
    }
}
