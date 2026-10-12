using CommunityToolkit.Mvvm.ComponentModel;
using ImogeIndex.Common;
using ImogeIndex.Niraction;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using System.Windows.Media.Imaging;

namespace ImooneWPF.Modelle;

/// <summary>
/// ﾆﾗｸｼｮﾝﾃﾞーﾀ
/// </summary>
public partial class NiractionItem : ObservableObject
{
    public string Name { get; set; }
    public string First { get; set; }
    public string[] Values { get; set; }

    public Uri? Link { get; set; }

    [ObservableProperty]
    public partial BitmapSource? Thumbnail { get; set; }

    public long GlueId { get; set; } = 0;

    public NiractionItem(string name, string[] value) {
        Name = name;
        Values = value;
        First = Values[0];
        Prepare();
    }

    public NiractionItem(NiractionValue pair) {
        Name = pair.Name!;
        Values = pair.Values!;
        First = Values[0];
        Prepare();
    }

    void Prepare() {
        if (Uri.TryCreate(First, UriKind.Absolute, out var uri) &&
            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeFtp))
        {
            Link = uri;
        }
    }
}

/// <summary>
/// ﾆﾗｸｼｮﾝ
/// </summary>
public partial class NiractionWindowModelle : ObservableObject
{
    /// <summary>
    /// ﾆﾗｸｼｮﾝ
    /// </summary>
    public ObservableCollection<NiractionItem> Items { get; } = new();
}
