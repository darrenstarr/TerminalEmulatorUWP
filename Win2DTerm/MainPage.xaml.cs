using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace Win2DTerm
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        public MainPage()
        {
            InitializeComponent();

            foreach (System.Text.EncodingInfo ei in System.Text.Encoding.GetEncodings())
            {
                System.Text.Encoding e = ei.GetEncoding();

                System.Diagnostics.Debug.Write(ei.CodePage.ToString());
                if (ei.CodePage == e.CodePage)
                    System.Diagnostics.Debug.Write("    ");
                else
                    System.Diagnostics.Debug.Write("*** ");

                System.Diagnostics.Debug.Write("{0,-25}", ei.Name);
                if (ei.CodePage == e.CodePage)
                    System.Diagnostics.Debug.Write("    ");
                else
                    System.Diagnostics.Debug.Write("*** ");

                System.Diagnostics.Debug.Write("{0,-25}", ei.DisplayName);
                if (ei.CodePage == e.CodePage)
                    System.Diagnostics.Debug.Write("    ");
                else
                    System.Diagnostics.Debug.Write("*** ");

                System.Diagnostics.Debug.WriteLine("");
            }
        }

        private void OnHostnameTapped(object sender, TappedRoutedEventArgs e)
        {
            FlyoutBase.ShowAttachedFlyout((FrameworkElement)sender);
        }

        private void ConnectTapped(object sender, TappedRoutedEventArgs e)
        {
            terminal.ConnectToSsh(Hostname.Text, Convert.ToInt32(Port.Text), Username.Text, Password.Text);
        }
    }
}
