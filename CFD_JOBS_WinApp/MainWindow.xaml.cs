using CFD_JOBS_WinApp.Interface;
using CFD_JOBS_WinApp.Jobs;
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

namespace CFD_JOBS_WinApp
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        List<BaseCFDJob> jobs = new List<BaseCFDJob>();
        public MainWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            jobs.Add(new AyondoTradeHistoryJob("Trade History"));
            jobs.Add(new AyondoTradeHistoryLiveJob("Trade History Live"));
            tabJobs.ItemsSource = jobs;

            tabJobs.SelectedIndex = 0;
        }

        private void BtnStart_Click(object sender, RoutedEventArgs e)
        {
            jobs.ForEach(item => ((ICFDJob)item).Run());
        }

        private void BtnStop_Click(object sender, RoutedEventArgs e)
        {
            jobs.ForEach(item => ((ICFDJob)item).Stop());
        }
    }
}
