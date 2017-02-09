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

namespace Shawbot
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private string timestamp = "";

        public MainWindow()
        {
            InitializeComponent();
            btnStart.Focus();
            int filenumber = Shawbot.Properties.Settings.Default.Fileno;
            int lastline = Shawbot.Properties.Settings.Default.Lastline;
        }

        private void btnStart_Click(object sender, RoutedEventArgs e)
        {
            btnStart.IsEnabled = false;
            btnStop.IsEnabled = true;
            btnPause.IsEnabled = true;
            btnPause.Focus();
            btnReset.IsEnabled = false;
            ckNoTweet.IsEnabled = false;
            timestamp = DateTime.Now.ToString();
            lblTimestamp.Content = "(running since " + timestamp + ")";
        }

        private void btnStop_Click(object sender, RoutedEventArgs e)
        {
            btnStart.IsEnabled = true;
            btnStop.IsEnabled = false;
            btnPause.IsEnabled = false;
            btnReset.IsEnabled = true;
            ckNoTweet.IsEnabled = true;
            lblTimestamp.Content = "";
        }

        private void btnPause_Click(object sender, RoutedEventArgs e)
        {
            btnStart.IsEnabled = false;
            btnStop.IsEnabled = true;
            btnPause.IsEnabled = true;
            btnReset.IsEnabled = false;
            ckNoTweet.IsEnabled = false;
            string content = (string)btnPause.Content;
            if ("Pause".Equals(content))
            {
                lblTimestamp.Content = "(paused)";
                btnPause.Content = "Resume";
            }
            else
            {
                lblTimestamp.Content = timestamp;
                btnPause.Content = "Pause";
            }
        }

        private void btnReset_Click(object sender, RoutedEventArgs e)
        {
            // reset parameters (current file # to 1, current line # to 1)
            Shawbot.Properties.Settings.Default.Fileno = 0;
            Shawbot.Properties.Settings.Default.Lastline = 0;
            Shawbot.Properties.Settings.Default.Save();
        }
    }
}
