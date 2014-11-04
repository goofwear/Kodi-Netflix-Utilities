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

using Awesomium.Core;
using WindowsInput;
using System.Configuration;

namespace NetflixBrowser
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private string cachepath = "BrowserCache";


        private void Window_Loaded(object sender, RoutedEventArgs e)
        {

            initSettings();

            WebSession session = WebCore.CreateWebSession(cachepath, new WebPreferences() { SmoothScrolling = true, CustomCSS = "body{ background-color: #000;}" });

            OSM.WebSession = session;

            string targetURL = "http://www.netflix.com/WiPlayer?movieid=" + App.Args["/movieid"] + "&amp;trkid=" + App.Args["/trackid"];
            OSM.Source = new Uri(targetURL);
        }

        private void initSettings()
        {
            if (ConfigurationManager.AppSettings.Get("browser_cache_location") == null)
            {
                ConfigurationManager.AppSettings.Set("browser_cache_location", cachepath);
            }
            else
            {
                cachepath = ConfigurationManager.AppSettings.Get("browser_cache_location");
            }
        }



        private void OSM_AddressChanged(object sender, UrlEventArgs e)
        {
 

            System.Windows.Threading.DispatcherTimer dispatcher = new System.Windows.Threading.DispatcherTimer();
            dispatcher.Tick += new EventHandler(KeepFocus);
            dispatcher.Interval = new TimeSpan(0, 0, 1);

            if (OSM.Source.ToString().Contains("?movieid=" + App.Args["/movieid"] + "&amp;trkid=" + App.Args["/trackid"]))
            {
                dispatcher.Start();
                OSM.Visibility = System.Windows.Visibility.Visible;
            }
            else
            {
                dispatcher.Stop();
            }

        }

        private void KeepFocus(object sender, EventArgs e)
        {
            WindowsInput.InputSimulator sim = new WindowsInput.InputSimulator();
            double winx = this.Left;
            double winy = this.Top;
           

            sim.Mouse.MoveMouseTo(winx + 100, winy + 100);
            sim.Mouse.LeftButtonClick();
            sim.Mouse.MoveMouseTo(1000,65535);

        }



    }
}
