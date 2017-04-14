using CefSharp.Wpf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace ChromeNet
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        ChromiumWebBrowser webView = null;
        public MainWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            var setting = new CefSharp.CefSettings();
            CefSharp.Cef.Initialize(setting);

            webView = new CefSharp.Wpf.ChromiumWebBrowser();
            borderChrome.Child = webView;
            webView.FrameLoadEnd += WebView_FrameLoadEnd;

            txtUrl.Text = "https://www.baidu.com";
        }

        private void WebView_FrameLoadEnd(object sender, CefSharp.FrameLoadEndEventArgs e)
        {
            if (e.Frame.IsMain)
            {
                //e.Frame.ViewSource();
                e.Frame.GetTextAsync().ContinueWith(task => {
                    var html = task.Result;
                    MessageBox.Show("返回字符串总长度:" + html.Length.ToString());
                });
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            webView.Address = txtUrl.Text;
        }
    }
}
