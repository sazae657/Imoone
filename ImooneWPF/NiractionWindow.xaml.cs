using ImogeIndex;
using ImogeIndex.Common;
using ImogeIndex.Niraction;
using ImogeIndex.Web3;
using ImooneWPF.Modelle;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;

namespace ImooneWPF;

/// <summary>
/// NiractionWindow.xaml の相互作用ﾛｼﾞｯｸ
/// </summary>
public partial class NiractionWindow : Nuendow
{
    public NiractionWindow()
    {
        InitializeComponent();
        Closed += (_, _) => {
            glueTokenSource?.Dispose();
            glueTokenSource = null;
        };
        Closing += (_, _) => {
            closing = true;
            glueTokenSource?.Cancel();
        };
    }

    NiractionWindowModelle DC => (DataContext as NiractionWindowModelle)!;
    CancellationTokenSource? glueTokenSource;
    bool closing = false;

    /// <summary>
    /// ﾆﾗｸｼｮﾝ応答を裁く
    /// </summary>
    /// <param name="niraction"></param>
    public async void Niraction(NiractionResponse niraction)
    {
        if (closing) {
            return;
        }

        // 前回のﾘｸｴｽﾄを中止し、ﾘｸｴｽﾄ単位の新しいﾄーｸﾝに差し替える
        var old = glueTokenSource;
        old?.Cancel();
        old?.Dispose();
        var cts = new CancellationTokenSource();
        glueTokenSource = cts;
        var token = cts.Token;

        DC.Items.Clear();
        foreach (var x in niraction.Values) {
            DC.Items.Add(new NiractionItem(x));
        }

        var ids = new List<long>();
        foreach (var m in DC.Items) {
            if (m.Values.Length < 2) {
                continue;
            }
            if (long.TryParse(m.Values[1], out var c)) {
                ids.Add(c);
                m.GlueId = c;
            }
        }
        await SendGlue(ids, token);
    }

    /// <summary>
    /// Glue送信
    /// </summary>
    /// <param name="ids"></param>
    /// <param name="token">このﾘｸｴｽﾄ専用のﾄーｸﾝ</param>
    /// <returns></returns>
    async Task SendGlue(IEnumerable<long> ids, CancellationToken token)
    {
        if (closing || token.IsCancellationRequested) {
            return;
        }
        try {
            if (!Profile.Active.Niraction.Enable) {
                return;
            }
            var nir = new NiractionClient(Profile.Active.Niraction);
            var sm = await nir.Glue(ids, token);
            if (!closing && sm != null) {
                await ProcessGlue(sm, token);
            }
        }
        catch (OperationCanceledException) {
            // たぶん再ﾘｸｴｽﾄ割り込み
        }
        catch (Exception ex) {
            if (!token.IsCancellationRequested) {
                Debug.WriteLine(ex);
            }
        }
    }

    /// <summary>
    /// Glueの結果回収
    /// </summary>
    /// <param name="glue"></param>
    /// <param name="token"></param>
    async Task ProcessGlue(NiractionGlueResponse glue, CancellationToken token)
    {
        foreach (var x in glue.Values) {
            if (closing || token.IsCancellationRequested) {
                return;
            }
            if (x.Image == null) {
                continue;
            }
            using (var mem = new MemoryStream(x.Image)) {
                var image = new BitmapImage();
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.StreamSource = mem;
                image.EndInit();
                image.Freeze();
                foreach (var ip in DC.Items.Where(w => w.GlueId == x.Id)) {
                    ip.Thumbnail = image;
                }
            }
        }
    }

    private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        e.Handled = true;
        if (e.Uri.Scheme != Uri.UriSchemeHttp &&
            e.Uri.Scheme != Uri.UriSchemeHttps &&
            e.Uri.Scheme != Uri.UriSchemeFtp) {
            return;
        }

        Process.Start(new ProcessStartInfo {
            FileName = e.Uri.AbsoluteUri,
            UseShellExecute = true
        });
    }

    /// <summary>
    /// ｺﾋﾟｨ
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void CopyCommand_Executed(object sender, ExecutedRoutedEventArgs e)
    {
        var field = e.Parameter as NiractionItem;
        if (field == null) {
            return;
        }
        Clipboard.SetText($"{field.Name}|{field.First}");
    }

}
