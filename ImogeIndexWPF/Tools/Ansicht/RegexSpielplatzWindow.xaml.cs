using ImogeIndexWPF.Tools.Modelle;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Threading;

namespace ImogeIndexWPF.Tools.Ansicht;

/// <summary>
/// RegexSpielplatzWindow.xaml の相互作用ﾛｼﾞｯｸ
/// </summary>
public partial class RegexSpielplatzWindow : ImogeIndex.Web3.Nuendow {
    public RegexSpielplatzWindow() {
        InitializeComponent();
        Loaded += RegexTesterPage_Loaded;
        editField.Document = new FlowDocument();
    }

    public void SetRegex(string r)
    {
        if (DataContext is not RegexSpielplatzModelle modell) {
            return;
        }
        modell.Regex = r;
    }

    record PairMat(Brush Foreground, Brush Background);

    List<PairMat> brushMats = new();

    Regex? regex;
    string? text;

    private void RegexTesterPage_Loaded(object sender, RoutedEventArgs e) {
        if (DataContext is not RegexSpielplatzModelle modell) {
            return;
        }
        modell.OnProcessRegex += Modell_OnProcessRegex;
        modell.OnEvaluate += Modell_OnEvaluate;
        if (brushMats.Count == 0) {
            brushMats.AddRange(
            Enumerable.Sequence(1, 255, 42).Select(w => {
                var mc = CreateBCGYR((double)w / 255.0);
                return new PairMat(
                     CreateReadableForeground(mc),
                    new SolidColorBrush(mc)
                );
            }));
        }
    }

    private void Modell_OnEvaluate(string str) {
        text = str;
        if (regex == null || string.IsNullOrEmpty(text)) {
            editField.Document.Blocks.Clear();
            return;
        }
        try {
            Evaluate(regex, text);
        }
        catch (Exception r) {
            ClearResults();
            regex = null;
            editField.BeginChange();
            editField.Document.Blocks.Clear();
            var text = new Bold(new Run(r.Message));
            text.Foreground = Brushes.Red;
            var paragraph = new Paragraph();
            paragraph.Inlines.Add(text);
            editField.Document.Blocks.Add(paragraph);
            editField.EndChange();
        }
    }

    private void Modell_OnProcessRegex(string src) {
        if (DataContext is not RegexSpielplatzModelle modell) {
            return;
        }

        try {
            regex = new Regex(src,
                modell.IgnoreCase ? RegexOptions.Compiled|RegexOptions.IgnoreCase : RegexOptions.Compiled, TimeSpan.FromSeconds(1));
            editField.Document.Blocks.Clear();
            Evaluate(regex, text);
        }
        catch (Exception r) {
            ClearResults();
            regex = null;
            editField.BeginChange();
            editField.Document.Blocks.Clear();
            var text = new Bold(new Run(r.Message));
            text.Foreground = Brushes.Red;
            var paragraph = new Paragraph();
            paragraph.Inlines.Add(text);
            editField.Document.Blocks.Add(paragraph);
            editField.EndChange();
        }
    }

    /// <summary>
    /// Groupのﾀﾞﾝﾌﾟを消す
    /// </summary>
    void ClearResults() {
        if (DataContext is not RegexSpielplatzModelle modell) {
            return;
        }
        modell.Results.Clear();
    }

    /// <summary>
    /// 背景色を作る
    /// </summary>
    /// <param name="indy"></param>
    /// <returns></returns>
    static Color CreateBCGYR(double indy) {
        double volume = indy;
        double pour = Math.Cos(4 * Math.PI * volume);
        int cpi = (int)((-pour / 2 + 0.5) * 255);

        var (r, g, b) = volume switch
        {
            >= 1.00 => (255, 0, 0),
            >= 0.75 => (255, cpi, 0),
            >= 0.50 => (cpi, 255, 0),
            >= 0.25 => (0, 255, cpi),
            >= 0.00 => (0, cpi, 255),
            _ => (0, 0, 255)
        };
        return Color.FromRgb((byte)r, (byte)g, (byte)b);
    }

    /// <summary>
    /// 背景色からそれっぽい前景色を出す
    /// </summary>
    /// <param name="bg"></param>
    /// <returns></returns>
    static SolidColorBrush CreateReadableForeground(Color bg) {
        double r = bg.R / 255.0;
        double g = bg.G / 255.0;
        double b = bg.B / 255.0;

        r = r <= 0.03928 ? r / 12.92 : Math.Pow((r + 0.055) / 1.055, 2.4);
        g = g <= 0.03928 ? g / 12.92 : Math.Pow((g + 0.055) / 1.055, 2.4);
        b = b <= 0.03928 ? b / 12.92 : Math.Pow((b + 0.055) / 1.055, 2.4);
        double lum = 0.2126 * r + 0.7152 * g + 0.0722 * b;
        return lum > 0.179 ? Brushes.Black : Brushes.White;
    }

    /// <summary>
    /// 正規表現の評価＆表示
    /// </summary>
    /// <param name="re"></param>
    /// <param name="s"></param>
    void Evaluate(Regex re, string? s) {
        if (string.IsNullOrEmpty(s)) {
            return;
        }
        if (DataContext is not RegexSpielplatzModelle modell) {
            return;
        }
        ClearResults();

        editField.BeginChange();
        editField.Document.Blocks.Clear();
        var paragraph = new Paragraph();

        MatchCollection matches = re.Matches(s);
        int currentIndex = 0;

        paragraph.Inlines.Add(new Run($"Match: {matches.Count}"));
        paragraph.Inlines.Add(new LineBreak());

        foreach (var match in matches.Cast<Match>()) {
            var smi = match.Groups.Count == 1 ? 0 : 1;
            for (int i = smi; i < match.Groups.Count; i++) {
                Group g = match.Groups[i];
                if (g.Success) {
                    foreach (var c in g.Captures.Cast<Capture>()) {
                        modell.Results.Add(new Kabaddiing(g.Name, c.Index, c.Value));
                    }
                }
            }
        }

        foreach (var match in matches.Cast<Match>()) {
            if (match.Index > currentIndex) {
                paragraph.Inlines.Add(new Run(s.Substring(currentIndex, match.Index - currentIndex)));
            }

            var charGroupLevel = new int[match.Length];
            for (int i = 1; i < match.Groups.Count; i++) {
                Group g = match.Groups[i];
                if (g.Success) {
                    int startIndex = g.Index - match.Index;
                    for (int j = 0; j < g.Length; j++) {
                        if (startIndex + j >= 0 && startIndex + j < charGroupLevel.Length) {
                            charGroupLevel[startIndex + j] = i;
                        }
                    }
                }
            }

            if (match.Length > 0) {
                var currentLevel = charGroupLevel[0];
                var chunkStart = 0;

                for (int i = 1; i <= match.Length; i++) {
                    if (i == match.Length || charGroupLevel[i] != currentLevel) {
                        var chunkLength = i - chunkStart;
                        var chunkText = match.Value.Substring(chunkStart, chunkLength);

                        // 色の決定
                        var brush = currentLevel == 0
                            ? brushMats[0]
                            : brushMats[(currentLevel - 1) % (brushMats.Count - 1) + 1];
                        var run = new Run(chunkText)
                        {
                            Background = brush.Background,
                            Foreground = brush.Foreground,
                            FontWeight = FontWeights.Bold,
                            ToolTip = currentLevel == 0 ? "Match" : $"Group {currentLevel}"
                        };

                        paragraph.Inlines.Add(run);

                        if (i < match.Length) {
                            currentLevel = charGroupLevel[i];
                            chunkStart = i;
                        }
                    }
                }
            }
            currentIndex = match.Index + match.Length;
        }

        if (currentIndex < s.Length) {
            paragraph.Inlines.Add(new Run(s.Substring(currentIndex)));
        }

        editField.Document.Blocks.Add(paragraph);
        editField.EndChange();
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () => AdjustListViewColumns(LustView));
    }

    /// <summary>
    /// ﾘｽﾄﾋﾞｭーのｶﾗﾑを揃えまくる
    /// </summary>
    /// <param name="listView"></param>
    private void AdjustListViewColumns(ListView listView) {
        var gridView = listView.View as GridView;
        if (gridView != null) {
            foreach (var column in gridView.Columns) {
                if (double.IsNaN(column.Width)) {
                    column.Width = column.ActualWidth;
                }
                column.Width = double.NaN;
            }
        }
    }
}

