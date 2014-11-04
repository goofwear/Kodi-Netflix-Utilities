using System;
using System.Collections;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Net;

namespace NetflixBrowser
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public static Hashtable Args = new Hashtable();

        private void Application_Startup(object sender, StartupEventArgs e)
        {
            if (e.Args.Length > 0)
            {
                string pattern = @"(?<argname>/\w+)=(?<argvalue>[A-Za-z0-9@_\.]+)";

                foreach (string arg in e.Args)
                {
                    Match match = Regex.Match(arg, pattern);
                    if (!match.Success) throw new ArgumentException("The command line arguments are improperly formed. Use /argname=argvalue.");

                    Args.Add(match.Groups["argname"].Value, match.Groups["argvalue"].Value);
                }
            }
        }



        private void Application_Exit(object sender, ExitEventArgs e)
        {





        }

        
    }
}
