using ImogeIndex.Web3;
using ImooneWPF.Modelle;

namespace ImooneWPF;
/// <summary>
/// LabelEditWindow.xaml の相互作用ﾛｼﾞｯｸ
/// </summary>
public partial class LabelEditWindow : Nuendow
{
    public LabelEditWindow(ImogeIndex.Settings s)
    {
        InitializeComponent();
        DataContext = new LabelEditWindowModelle(s);
    }
}
