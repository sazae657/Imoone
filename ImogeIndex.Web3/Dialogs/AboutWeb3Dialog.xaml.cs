using ImogeIndex.Web3.Dialogs.Modelle;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace ImogeIndex.Web3.Dialogs
{
    /// <summary>
    /// AboutWeb3Dialog.xaml の相互作用ﾛｼﾞｯｸ
    /// </summary>
    public partial class AboutWeb3Dialog : Nuendow
    {
        public AboutWeb3Dialog()
        {
            InitializeComponent();
        }

        public void SetClientName(string name)
        {
            if (DataContext is not AboutWeb3DialogModelle modell) {
                return;
            }
            modell.ClientName = name;
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo {
                FileName = e.Uri.AbsoluteUri,
                UseShellExecute = true
            });
            e.Handled = true;
        }

    }
}
