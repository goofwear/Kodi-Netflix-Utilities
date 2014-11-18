using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Net;
using System.IO;
using System.Text.RegularExpressions;

namespace UpdateSeriesData
{
    class Program
    {
        private static CookieContainer cookies;
        private static string loginurl = "https://signup.netflix.com/login";

        static string GetAuth()
        {
            // setup the web request


            HttpWebRequest webreq = (HttpWebRequest)WebRequest.Create(loginurl);
            webreq.CookieContainer = cookies;

            Stream objStream = webreq.GetResponse().GetResponseStream();
            StreamReader reader = new StreamReader(objStream);

            string content = reader.ReadToEnd();

            Match match = Regex.Match(content, "input.*?authURL.*?value.*?\"(.*?)\"");

            return match.Groups[1].Value;
        }

        static void Login(string username, string password)
        {
            // setup the post data
            ASCIIEncoding encoding = new ASCIIEncoding();
            string postdata = "email=" + username + "&password=" + password + "&authURL=" + GetAuth();
            byte[] data = encoding.GetBytes(postdata);

            // setup the web request
            HttpWebRequest webreq = (HttpWebRequest)WebRequest.Create(loginurl);
            webreq.CookieContainer = cookies;
            webreq.Method = "POST";
            webreq.ContentType = "application/x-www-form-urlencoded";
            webreq.ContentLength = data.Length;
            Stream reqstream = webreq.GetRequestStream();
            reqstream.Write(data, 0, data.Length);
            reqstream.Close();
            try
            {
                Stream objStream = webreq.GetResponse().GetResponseStream();
                StreamReader reader = new StreamReader(objStream);

                string response = reader.ReadToEnd();
            }
            catch { }
            
            //Console.Write(response);
        }


        static void Main(string[] args)
        {

            Hashtable Args = new Hashtable();



            if (args.Length > 0)
            {
                string pattern = @"(?<argname>/\w+)=(?<argvalue>[A-Za-z0-9@_\.:\\]+)";

                foreach (string arg in args)
                {
                    Match match = Regex.Match(arg, pattern);
                    if (!match.Success) throw new ArgumentException("The command line arguments are improperly formed. Use /argname=argvalue.");

                    Args.Add(match.Groups["argname"].Value.ToUpper(), match.Groups["argvalue"].Value);
                }
            }

            cookies = new CookieContainer();

            Login(Args["/UN"].ToString(), Args["/PW"].ToString());

            if (Args["/SERIESID"] != null)
            {
                string seasonUrl = "http://api-global.netflix.com/desktop/odp/episodes?forceEpisodes=true&routing=redirect&video={0}";
                string url = String.Format(seasonUrl, Args["/SERIESID"].ToString());

                HttpWebRequest webreq = (HttpWebRequest)WebRequest.Create(url);
                webreq.CookieContainer = cookies;


                Stream objStream = webreq.GetResponse().GetResponseStream();
                StreamReader reader = new StreamReader(objStream);

                string response = reader.ReadToEnd();


                string saveMetaPath = Args["/SAVEPATH"].ToString();
                if (!saveMetaPath.EndsWith(@"\"))
                    saveMetaPath += @"\";
                //if (!saveMetaPath.EndsWith(@"\")) saveMetaPath += @"\";

                //saveMetaPath += @"Titles\" + Args["/SERIESID"].ToString() + @"\seasondata.json";
                File.WriteAllText(saveMetaPath + "seasonddata.json", response);


                string searchstring = response.Substring(response.IndexOf("\"episodes\":"));
                string episodeExpresssion = "{\"title\":\"(.*?)\",\"season\":(.*?),\"seasonYear\":.*?,\"episode\":(.*?),\"synopsis\":\".*?\",\"seasonId\":.*?,\"episodeId\":.*?,\"videoId\":.*?,\"nonMemberViewable\":.*?,\"runtime\":.*?,\"availableForED\":.*?,\"availabilityMessage\":.*?,\"stills\":\\[(.*?)\\],\"bookmarkPosition\":(.*?),\"lastModified\":\".*?\"}";
                MatchCollection matches = Regex.Matches(searchstring, episodeExpresssion);

                //Console.WriteLine(matches.Count);

                foreach (Match mtch in matches)
                {
                    if (!Directory.Exists(saveMetaPath + @"\Season " + mtch.Groups[2].Value))
                        Directory.CreateDirectory(saveMetaPath + @"\Season " + mtch.Groups[2].Value);

                    string episode = mtch.Groups[3].Value;
                    string season = mtch.Groups[2].Value;


                    if (episode.Length < 2)
                        episode = episode.PadLeft(2, '0');
                    if (season.Length < 2)
                        season = season.PadLeft(2, '0');

                    string filename = "S" + season + "E" + episode;

                    foreach (char illegal in Path.GetInvalidFileNameChars())
                    {
                        filename = filename.Replace(illegal.ToString(), "");
                    }

                    File.WriteAllText(saveMetaPath + @"\Season " + mtch.Groups[2].Value + @"\" + filename + ".json", mtch.Value);

                }

            }
        }
    }
}
