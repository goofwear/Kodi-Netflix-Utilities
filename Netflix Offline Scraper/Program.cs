using System;
using System.Configuration;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

namespace AvalonProjects.Kodi.Netflix.Scraper
{
    enum StatusType
    {
        Running, Done, Failed
    }

    enum RunningJob{
        Init = 3, Genres = 4, GenreTitles = 5, Titles = 6, Covers = 7, Series = 8, MyList = 9
    }

    class Program
    {
        static Configuration config;

        const int barLocation = 14;

        private static List<DateTime> RequestStack;
        private static int MaxRequestCount = 50;

        private static bool runPatience = false;
        private static bool stillProcessing = false;
        private static int patienceCursorLeft = 0;
        private static bool aborted = false;

        private static string username = "";
        private static string password = "";
        private static string auth = "";

        private const string starturl = "http://www.netflix.com";
        private const string loginurl = "https://signup.netflix.com/login";
        private static string apiurl = string.Empty;
        private const string seasonUrl = "http://api-global.netflix.com/desktop/odp/episodes?forceEpisodes=true&routing=redirect&video={0}";


        private static string rootpath = "";
        private static string backuppath = "";
        private static string backupinstance = "";

        private static CookieContainer cookies;

        private static List<string> genreList;
        private static Dictionary<string, string> titlelist;
        private static List<string> serieslist;
        private static Dictionary<string, Image> covers;


        static void cleanRequestStack()
        {
            if (RequestStack == null)
                RequestStack = new List<DateTime>();

            while(!aborted)
                try
                {
                    foreach (DateTime time in RequestStack.ToList<DateTime>())
                    {
                        if (time.AddMinutes(1) < DateTime.Now)
                        {
                            RequestStack.Remove(time);
                            break;
                        }
                    }
                }
                catch { }
        }

        static void drawUI()
        {
            Console.Clear();
            Console.WriteLine();
            Console.WriteLine("  Scraping Netflix for metadata.");
            Console.WriteLine();
            Console.WriteLine("    [ ] Initialize");
            Console.WriteLine("    [ ] Get genre data");
            Console.WriteLine("    [ ] Map genre titles");
            Console.WriteLine("    [ ] Get title details");
            Console.WriteLine("    [ ] Get title covers");
            Console.WriteLine("    [ ] Get extra series data");
            Console.WriteLine("    [ ] My List");
            Console.WriteLine();
            Console.WriteLine("  ╔════════════════════════════════════════════════════════════════════════╗");
            Console.WriteLine("  ║ Progress                                                               ║");
            Console.WriteLine("  ╠════════════════════════════════════════════════════════════════════════╣");
            Console.WriteLine("  ║                                                                        ║");
            Console.WriteLine("  ╚════════════════════════════════════════════════════════════════════════╝");

            Console.CursorVisible = false;
        }

        

        static void drawPatience(int barlength)
        {
            // store the current cursor position
            int top = Console.CursorTop;
            int left = Console.CursorLeft;

            int counter = 0;

            stillProcessing = true;

            do
            {
                if (counter >= barlength)
                    counter = 0;

                patienceCursorLeft = counter;

                Console.CursorTop = barLocation;
                Console.CursorLeft = 4;

                Console.BackgroundColor = ConsoleColor.DarkGray;
                for (int i = 0; i < counter; i++) Console.Write(" ");
                Console.BackgroundColor = ConsoleColor.Yellow;
                Console.Write(" ");
                Console.BackgroundColor = ConsoleColor.DarkGray;
                for (int i = counter + 1; i < barlength; i++) Console.Write(" ");
                Thread.Sleep(100);
                counter++;
                Console.CursorTop = top;
                Console.CursorLeft = left;
                Console.ResetColor();
            } while (runPatience && stillProcessing);


            Console.CursorLeft = left;
            Console.CursorTop = top;
            stillProcessing = false;
        }

        private static void DrawProgressBar(int complete, int maxVal, int barSize, char progressCharacter)
        {
            int top = Console.CursorTop;

            Console.CursorTop = barLocation;

            Console.CursorVisible = false;
            int left = Console.CursorLeft;
            Console.CursorLeft = 4;


            decimal perc = (decimal)complete / (decimal)maxVal;
            int chars = (int)Math.Floor(perc / ((decimal)1 / (decimal)barSize));
            if (perc == 1) chars = barSize;
            string p1 = String.Empty, p2 = String.Empty;

            string label = (perc * 100).ToString("N2") + "%";

            if (label.Length % 2 != 0)
                label = " " + label;
            //for (int i = 0; i < chars; i++) p1 += progressCharacter;


            int startwriting = (barSize / 2) - (label.Length / 2);

            for (int i = 0; i < chars; i++)
            {
                if (i < startwriting)
                    p1 += progressCharacter;
                else
                {
                    if (label.Length > 0)
                    {
                        p1 += label.Substring(0, 1);
                        if (label.Length > 1)
                            label = label.Substring(1);
                        else
                            label = string.Empty;

                    }
                    else
                        p1 += progressCharacter;

                }
            }

            for (int i = 0; i < barSize - chars; i++)
            {
                if (i + chars < startwriting)
                    p2 += progressCharacter;
                else
                {
                    if (label.Length > 0)
                    {
                        p2 += label.Substring(0, 1);
                        if (label.Length > 1)
                            label = label.Substring(1);
                        else
                            label = string.Empty;

                    }
                    else
                        p2 += progressCharacter;
                }
            }



            Console.ForegroundColor = ConsoleColor.Yellow;

            Console.BackgroundColor = ConsoleColor.Blue;

            Console.Write(p1);
            Console.BackgroundColor = ConsoleColor.DarkGray ;
            Console.Write(p2);

            Console.ResetColor();

            //Console.Write(" {0}%", (perc * 100).ToString("N2"));
            Console.CursorLeft = left;

            Console.CursorTop = top;
        }

        static void clearProgress(int barlength)
        {
            Console.ResetColor();
            int top = Console.CursorTop;
            int left = Console.CursorLeft;

            Console.CursorTop = barLocation;
            Console.CursorLeft = 4;
            for (int i = 0; i < barlength; i++) Console.Write(" ");

            Console.CursorTop = top;
            Console.CursorLeft = left;

            patienceCursorLeft = 4;
        }


        static void EnsurePaths()
        {
            if (rootpath != "")
                if (!rootpath.EndsWith(@"\"))
                    rootpath += @"\";

            backuppath = rootpath + @"meta-bak\";
            rootpath += @"meta\";

            if (!Directory.Exists(rootpath))
                Directory.CreateDirectory(rootpath);

            if (!Directory.Exists(backuppath))
                Directory.CreateDirectory(backuppath);

            if (!Directory.Exists(rootpath + "Genres"))
                Directory.CreateDirectory(rootpath + "Genres");
            if (!Directory.Exists(rootpath + "GenreTitles"))
                Directory.CreateDirectory(rootpath + "GenreTitles");
            if (!Directory.Exists(rootpath + "Titles"))
                Directory.CreateDirectory(rootpath + "Titles");
            if (!Directory.Exists(rootpath + "MyList"))
                Directory.CreateDirectory(rootpath + "MyList");
        }


        static void setStatus(RunningJob job, StatusType status)
        {
            int left = Console.CursorLeft;
            int top = Console.CursorTop;

            Console.CursorLeft = 5;
            Console.CursorTop = (int)job;

            Console.ResetColor();

            switch (status)
            {
                case StatusType.Done:
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.Write("=");
                    break;
                case StatusType.Failed:
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.Write("X");
                    break;
                case StatusType.Running:
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.Write("►");
                    break;
                default:
                    Console.Write("?");
                    break;
            }

            Console.ResetColor();
            Console.CursorTop = top;
            Console.CursorLeft = left;
        }

        static void Main(string[] args)
        {
            // create a thread to empty the request stack#
            config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
            Thread cleanRequests = new Thread(() => cleanRequestStack());
            cleanRequests.Start();

            cookies = new CookieContainer();
            genreList = new List<string>();
            titlelist = new Dictionary<string, string>();
            serieslist = new List<string>();
            covers = new Dictionary<string, Image>();


            username = ConfigurationManager.AppSettings.Get("UserName");
            password = ConfigurationManager.AppSettings.Get("Password");

            while (string.IsNullOrEmpty(username))
            {
                getUserName();
            }

            while (string.IsNullOrEmpty(password))
            {
                getPassword();
            }

            drawUI();



            Initialize();

            scrapeMyList();

            ScrapeGenres();

            mapGenreTitles();
            scrapeTitles();
            scrapeCovers();
            scrapeSeriesData();

            // stop tidying the request stack
            cleanRequests.Abort();


            clearProgress(70);
            Console.WriteLine();
            Console.WriteLine("  Press any key to exit...");
            Console.ReadKey();
            Console.Clear();
        }

        static void Initialize()
        {
            setStatus(RunningJob.Init, StatusType.Running);

            Thread patience = new Thread(() => drawPatience(70));
            patience.Start();

            runPatience = true;
            


            // Get settings
            // 1. Maximum # of requests
            int tmpReqCount = 0;
            if(!Int32.TryParse(ConfigurationManager.AppSettings.Get("RequestsPerMinute"), out tmpReqCount))
            {
                if (config.AppSettings.Settings["RequestsPerMinute"] == null)
                    config.AppSettings.Settings.Add(new KeyValueConfigurationElement("RequestsPerMinute", ""));
                config.AppSettings.Settings["RequestsPerMinute"].Value = MaxRequestCount.ToString();
                config.Save(ConfigurationSaveMode.Modified);
                ConfigurationManager.RefreshSection("appSettings");
            }
            else
            {
                MaxRequestCount = tmpReqCount;
            }



            auth = GetAuth();
            Login();

            EnsurePaths();
            backupfolder(rootpath);

            runPatience = false;
            while (stillProcessing) ;



            setStatus(RunningJob.Init, StatusType.Done);
        }

        static void ScrapeGenres()
        {

            setStatus(RunningJob.Genres, StatusType.Running);
            Thread patience = new Thread(() => drawPatience(70));
            patience.Start();

            runPatience = true;

            while (RequestStack.Count >= MaxRequestCount) ;

            RequestStack.Add(DateTime.Now);

            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(starturl);
            req.CookieContainer = cookies;

            string content = string.Empty;

            using (Stream objStream = req.GetResponse().GetResponseStream()) 
            {
                using (StreamReader reader = new StreamReader(objStream))
                {
                    content = reader.ReadToEnd();
                }
            }

            MatchCollection matches = Regex.Matches(content, "<li><a href=\"(.*?)WiGenre\\?agid=(.*?)\">(.*?)</a></li>");

            string genres = string.Empty;

            foreach (Match genre in matches)
            {
                string url = genre.Groups[1].Value + "WiGenre?agid=" + genre.Groups[2].Value;
                string id = genre.Groups[2].Value;
                string name = genre.Groups[3].Value;

                if (genres != string.Empty)
                    genres += "," + Environment.NewLine;

                genres += "'" + name + "':'" + id + "'";

                genreList.Add(id);

                ScrapeSubGenre(id, url);
            }

            if (genres != string.Empty)
            {
                genres = "Genres = {" + Environment.NewLine + genres + Environment.NewLine + "}";

                File.WriteAllText(rootpath + @"Genres\genres.json", genres);
            }



            runPatience = false;
            while (stillProcessing) ;

            setStatus(RunningJob.Genres, StatusType.Done);
        }

        static void ScrapeSubGenre(string genreId, string url)
        {
            while (RequestStack.Count >= MaxRequestCount) ;

            RequestStack.Add(DateTime.Now);

            HttpWebRequest webreq = (HttpWebRequest)WebRequest.Create(url);
            webreq.CookieContainer = cookies;

            try
            {
                Stream objStream = webreq.GetResponse().GetResponseStream();
                StreamReader reader = new StreamReader(objStream);

                string content = reader.ReadToEnd();

                string apimatchpattern = "\"BUILD_IDENTIFIER\":\"(.*?)\",\"host\":\".*?\",\"SHAKTI_API_ROOT\":\"(.*?)\"";
                Match apimatch = Regex.Match(content, apimatchpattern);

                apiurl = apimatch.Groups[2] + @"/" + apimatch.Groups[1];
                if (content.Contains("<div id=\"subGenres\""))
                {
                    int start = content.IndexOf("<div id=\"subGenres\"");
                    int end = content.IndexOf("</div>", start);
                    content = content.Substring(start, end - start);



                    MatchCollection matches = Regex.Matches(content, "<a.*?WiGenre\\?agid=(.*?)\\&.*?\">.*?<span>(.*?)</span>.*?</a>");

                    string genres = string.Empty;

                    foreach (Match genre in matches)
                    {
                        string thisid = genre.Groups[1].Value;
                        string name = genre.Groups[2].Value;

                        if (genres != string.Empty)
                            genres += "," + Environment.NewLine;
                        genres += "'" + name.Trim() + "':'" + thisid + "'";

                        genreList.Add(thisid);
                    }
                    if (genres != string.Empty)
                    {
                        genres = "Genres = {" + Environment.NewLine + genres + Environment.NewLine + "}";

                        File.WriteAllText(rootpath + @"Genres\" + genreId + ".json", genres);
                    }
                }
            }
            catch
            {
                if (url.StartsWith("http://www2"))
                    ScrapeSubGenre(genreId, url.Replace("http://www2", "http://www"));
                else
                    ScrapeSubGenre(genreId, url.Replace("http://www", "http://www2"));
            }

        }

        static void mapGenreTitles()
        {
            int genreCount = genreList.Count;
            int progress = 0;
            setStatus(RunningJob.GenreTitles, StatusType.Running);

            foreach (string key in genreList)
            {
                DrawProgressBar(progress, genreCount, 70, ' ');

                string url = apiurl + "/wigenre?genreId=" + key;
                int start = 0;
                int size = 100;
                string response = "";
                int counter = 0;
                StringBuilder sb = new StringBuilder();

                while (!response.StartsWith("{\"catalogItems\":[]}"))
                {
                    string requesturl = url + "&full=false&from=" + start.ToString() + "&to=" + (start + size).ToString();
                    start += size + 1;

                    while (RequestStack.Count >= MaxRequestCount) ;

                    RequestStack.Add(DateTime.Now);

                    HttpWebRequest webreq = (HttpWebRequest)WebRequest.Create(requesturl);
                    webreq.CookieContainer = cookies;

                    Stream objStream = webreq.GetResponse().GetResponseStream();
                    StreamReader reader = new StreamReader(objStream);

                    response = reader.ReadToEnd();

                    string expr = "{\"boxart\":\"(.*?)\",\"titleId\":(.*?),\"title\":\"(.*?)\",\"playerUrl\":\"(.*?)\",\"trackId\":(.*?)}";

                    MatchCollection matches = Regex.Matches(response, expr);

                    foreach (Match title in matches)
                    {
                        if (sb.ToString() != string.Empty)
                            sb.Append(",");

                        sb.Append(title.Value);

                        if (!Directory.Exists(rootpath + @"GenreTitles\" + key))
                            Directory.CreateDirectory(rootpath + @"GenreTitles\" + key);

                        //if(!titles.ContainsKey(title.Groups[2].Value))
                        //    titles.Add(title.Groups[2].Value, title.Groups[5].Value);
                        if (!titlelist.ContainsKey(title.Groups[2].Value))
                            titlelist.Add(title.Groups[2].Value, title.Groups[0].Value);

                        File.WriteAllText(rootpath + @"GenreTitles\" + key + @"\" + title.Groups[2] + ".json", title.Value);
                        counter++;
                    }
                }

                progress++;

                sb.Insert(0, "{\"catalogItems\":[");
                sb.Append("]}");

                File.WriteAllText(rootpath + @"\GenreTitles\" + key + ".json", sb.ToString());
            }
            DrawProgressBar(progress, genreCount, 70, ' ');
            setStatus(RunningJob.GenreTitles, StatusType.Done);
        }


        static void scrapeTitles()
        {
            setStatus(RunningJob.Titles, StatusType.Running);

            string expr = "{\"boxart\":\"(.*?)\",\"titleId\":(.*?),\"title\":\"(.*?)\",\"playerUrl\":\"(.*?)\",\"trackId\":(.*?)}";

            int titlescount = titlelist.Count;
            int counter = 0;

            foreach (string key in titlelist.Keys)
            {


                DrawProgressBar(counter, titlescount, 70, ' ');

                Match match = Regex.Match(titlelist[key], expr);



                scrapeTitle(match.Groups[2].Value, match.Groups[5].Value);

                counter++;

            }
            DrawProgressBar(counter, titlescount, 70, ' ');


            setStatus(RunningJob.Titles, StatusType.Done);
        }

        static void scrapeMyList()
        {
            setStatus(RunningJob.MyList, StatusType.Running);
            //http://www.netflix.com/MyList

            Thread patience = new Thread(() => drawPatience(70));
            patience.Start();

            runPatience = true;

            while (RequestStack.Count >= MaxRequestCount) ;

            RequestStack.Add(DateTime.Now);

            HttpWebRequest req = (HttpWebRequest)WebRequest.Create("http://www.netflix.com/MyList");
            req.CookieContainer = cookies;

            string content = string.Empty;

            using (Stream objStream = req.GetResponse().GetResponseStream())
            {
                using (StreamReader reader = new StreamReader(objStream))
                {
                    content = reader.ReadToEnd();
                }
            }

            //@"<div class="agMovie agMovie-lulg"><span id="dbs70143836_0" class="boxShot boxShot-166 inqueue  hoverPlay  bobbable vbox_70143836"><img class="boxShotImg hideBobBoxshot" alt="Breaking Bad" src="http://cdn1.nflximg.net/images/6601/4176601.jpg"><a id="b070143836_0" class="bobbable popLink hideBobBoxshot playLink full uitracking-state-visible" href="http://www.netflix.com/WiPlayer?movieid=70143836&amp;trkid=50279251&amp;tctx=-99%2C-99%2C98b71697-b9f2-4228-a288-677089415510-1653505" data-uitrack="70143836,50279251,null,null">&nbsp;</a></span></div>";
            string expr = "<div class=\"agMovie agMovie-lulg\">.*?<a.*?WiPlayer\\?movieid=(.*?)&";

            MatchCollection matches = Regex.Matches(content, expr);
            int counter = 0;
            foreach (Match match in matches)
            {
                counter++;
                File.WriteAllText(rootpath + @"\MyList\" + match.Groups[1].Value, counter.ToString());
            }

            runPatience = false;
            while (stillProcessing) ;

            setStatus(RunningJob.MyList, StatusType.Done);
        }

        static void scrapeCovers()
        {
            setStatus(RunningJob.Covers, StatusType.Running);

            string expr = "{\"boxart\":\"(.*?)\",\"titleId\":(.*?),\"title\":\"(.*?)\",\"playerUrl\":\"(.*?)\",\"trackId\":(.*?)}";

            int titlescount = titlelist.Count;
            int counter = 0;

            foreach (string key in titlelist.Keys)
            {


                DrawProgressBar(counter, titlescount, 70, ' ');

                Match match = Regex.Match(titlelist[key], expr);



                DownloadCoverArt(match.Groups[1].Value, match.Groups[2].Value);

                counter++;

            }
            DrawProgressBar(counter, titlescount, 70, ' ');


            setStatus(RunningJob.Covers, StatusType.Done);
        }

        static void scrapeTitle(string titleid, string trackid)
        {
            while (RequestStack.Count >= MaxRequestCount) ;

            RequestStack.Add(DateTime.Now);

            string url = apiurl + "/bob?titleid=" + titleid + "&trackid=" + titleid;

            HttpWebRequest webreq = (HttpWebRequest)WebRequest.Create(url);
            webreq.CookieContainer = cookies;


            Stream objStream = webreq.GetResponse().GetResponseStream();
            StreamReader reader = new StreamReader(objStream);

            string content = reader.ReadToEnd();

            string expression = "{\"isMovie\":(.*?),\"isShow\":(.*?),\"titleid\":(.*?),\"title\":\"(.*?)\",\"mdpLink\":\"(.*?)\",\"synopsis\":\"(.*?)\",\"year\":(.*?),\"profileName\":\"(.*?)\",\"trackId\":(.*?),\"showMyList\":(.*?),\"actors\":(.*?),\"inPlayList\":(.*?),\"maturityLabel\":\"(.*?)\",\"maturityLevel\":(.*?),\"averageRating\":(.*?),\"predictedRating\":(.*?),\"yourRating\":(.*?),\"numSeasons\":(.*?),\"creators\":(.*?),\"directors\":(.*?)}";

            Match match = Regex.Match(content, expression);


            if (match.Groups[2].Value == "true")
                if (!serieslist.Contains(content))
                    serieslist.Add(content);




            if (!Directory.Exists(rootpath + @"Titles\" + titleid))
                Directory.CreateDirectory(rootpath + @"Titles\" + titleid);



            File.WriteAllText(rootpath + @"Titles\" + titleid + @"\meta.json", content);
        }


        static void DownloadCoverArt(string url, string titleid)
        {
            while (RequestStack.Count >= MaxRequestCount) ;

            RequestStack.Add(DateTime.Now);

            HttpWebRequest webreq = (HttpWebRequest)WebRequest.Create(url);
            webreq.CookieContainer = cookies;
            webreq.AllowWriteStreamBuffering = true;



            Image img = Image.FromStream(webreq.GetResponse().GetResponseStream());
            if (!Directory.Exists(rootpath + @"Titles\" + titleid))
                Directory.CreateDirectory(rootpath + @"Titles\" + titleid);
            img.Save(rootpath + @"Titles\" + titleid + @"\folder.jpg");
            img.Save(rootpath + @"Titles\" + titleid + @"\coverart.jpg");


            Bitmap logo = new Bitmap("netflix_logo.png");

            Image newimage = new Bitmap(img.Width, img.Height);


            Graphics gra = Graphics.FromImage(newimage);

            float ratioX = ((float)img.Width / (float)logo.Width);

            int badgeWidth = (int)(logo.Width * ratioX);
            int badgeHeight = (int)(logo.Height * ratioX);

            gra.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            gra.DrawImage(img, new Rectangle(0, 0, img.Width, img.Height));
            gra.DrawImage(logo, new Rectangle(0, 0, badgeWidth, badgeHeight));



            newimage.Save(rootpath + @"Titles\" + titleid + @"\library.jpg");

            covers.Add(titleid, img);
        }

        static void scrapeSeriesData()
        {
            //http://api-global.netflix.com/desktop/odp/episodes?forceEpisodes=true&routing=redirect&video=70185014

            int seriescount = serieslist.Count;
            int counter = 0;

            setStatus(RunningJob.Series, StatusType.Running);

            foreach (string content in serieslist)
            {
                DrawProgressBar(counter, seriescount, 70, ' ');

                string expression = "{\"isMovie\":(.*?),\"isShow\":(.*?),\"titleid\":(.*?),\"title\":\"(.*?)\",\"mdpLink\":\"(.*?)\",\"synopsis\":\"(.*?)\",\"year\":(.*?),\"profileName\":\"(.*?)\",\"trackId\":(.*?),\"showMyList\":(.*?),\"actors\":(.*?),\"inPlayList\":(.*?),\"maturityLabel\":\"(.*?)\",\"maturityLevel\":(.*?),\"averageRating\":(.*?),\"predictedRating\":(.*?),\"yourRating\":(.*?),\"numSeasons\":(.*?),\"creators\":(.*?),\"directors\":(.*?)}";

                Match match = Regex.Match(content, expression);

                string titleid = match.Groups[3].Value;
                string title = match.Groups[4].Value;

                while (RequestStack.Count >= MaxRequestCount) ;

                RequestStack.Add(DateTime.Now); ;

                string url = String.Format(seasonUrl, titleid);

                HttpWebRequest webreq = (HttpWebRequest)WebRequest.Create(url);
                webreq.CookieContainer = cookies;


                Stream objStream = webreq.GetResponse().GetResponseStream();
                StreamReader reader = new StreamReader(objStream);

                string response = reader.ReadToEnd();

                if (!Directory.Exists(rootpath + @"Titles\" + titleid))
                    Directory.CreateDirectory(rootpath + @"Titles\" + titleid);

                File.WriteAllText(rootpath + @"Titles\" + titleid + @"\seasonddata.json", response);

                string searchstring = response.Substring(response.IndexOf("\"episodes\":"));
                string episodeExpresssion = "{\"title\":\"(.*?)\",\"season\":(.*?),\"seasonYear\":.*?,\"episode\":(.*?),\"synopsis\":\".*?\",\"seasonId\":.*?,\"episodeId\":.*?,\"videoId\":.*?,\"nonMemberViewable\":.*?,\"runtime\":.*?,\"availableForED\":.*?,\"availabilityMessage\":.*?,\"stills\":\\[(.*?)\\],\"bookmarkPosition\":(.*?),\"lastModified\":\".*?\"}";
                MatchCollection matches = Regex.Matches(searchstring, episodeExpresssion);

                //Console.WriteLine(matches.Count);

                foreach (Match mtch in matches)
                {
                    if (!Directory.Exists(rootpath + @"Titles\" + titleid + @"\Season " + mtch.Groups[2].Value))
                        Directory.CreateDirectory(rootpath + @"Titles\" + titleid + @"\Season " + mtch.Groups[2].Value);

                    string episode = mtch.Groups[3].Value;
                    string season = mtch.Groups[2].Value;

                    string stills = mtch.Groups[4].Value;
                    int stillSize = 0;
                    Image stillImage = null;
                    string stillsexpr = "{\"offset\":(.*?),\"sequence\":(.*?),\"type\":\"(.*?)\",\"url\":\"(.*?)\",\"height\":(.*?),\"width\":(.*?)}";

                    MatchCollection stillMatches = Regex.Matches(stills, stillsexpr);

                    foreach (Match still in stillMatches)
                    {
                        if (Int32.Parse(still.Groups[6].Value) > stillSize)
                        {
                            stillSize = Int32.Parse(still.Groups[6].Value);

                            while (RequestStack.Count >= MaxRequestCount) ;

                            RequestStack.Add(DateTime.Now);

                            HttpWebRequest imgReq = (HttpWebRequest)WebRequest.Create(still.Groups[4].Value);
                            webreq.CookieContainer = cookies;
                            webreq.AllowWriteStreamBuffering = true;

                            stillImage = Image.FromStream(imgReq.GetResponse().GetResponseStream());
                        }
                    }

                    if (episode.Length < 2)
                        episode = episode.PadLeft(2, '0');
                    if (season.Length < 2)
                        season = season.PadLeft(2, '0');

                    string filename = "S" + season + "E" + episode;

                    if (stillImage != null)
                    {
                        stillImage.Save(rootpath + @"Titles\" + titleid + @"\Season " + mtch.Groups[2].Value + @"\" + filename + ".jpg");
                    }

                    foreach (char illegal in Path.GetInvalidFileNameChars())
                    {
                        filename = filename.Replace(illegal.ToString(), "");
                    }

                    File.WriteAllText(rootpath + @"Titles\" + titleid + @"\Season " + mtch.Groups[2].Value + @"\" + filename + ".json", mtch.Value);

                }



                counter++;
            }
            setStatus(RunningJob.Series, StatusType.Done);

            DrawProgressBar(counter, seriescount, 70, ' ');

        }

        static void getUserName()
        {

            int top = Console.CursorTop;
            int left = Console.CursorLeft;
            Console.BackgroundColor = ConsoleColor.DarkGray;
            Console.Clear();
            drawUI();

            drawGetUserName();
            string un = string.Empty;
            ConsoleKeyInfo key;



            do
            {
                key = Console.ReadKey(true);

                // Backspace Should Not Work
                if (key.Key != ConsoleKey.Backspace && key.Key != ConsoleKey.Enter)
                {
                    un += key.KeyChar;
                    Console.Write(key.KeyChar);
                }
                else
                {
                    if (key.Key == ConsoleKey.Backspace && un.Length > 0)
                    {
                        un = un.Substring(0, (un.Length - 1));
                        Console.Write("\b \b");
                    }
                }
            }
            // Stops Receving Keys Once Enter is Pressed
            while (key.Key != ConsoleKey.Enter);

            if (config.AppSettings.Settings["UserName"] == null)
                config.AppSettings.Settings.Add(new KeyValueConfigurationElement("UserName", ""));

            config.AppSettings.Settings["UserName"].Value = un;
            config.Save(ConfigurationSaveMode.Modified);
            ConfigurationManager.RefreshSection("appSettings");
            username = ConfigurationManager.AppSettings.Get("UserName");
            


            Console.ResetColor();
            Console.Clear();
            drawUI();

        }

        static void drawGetUserName()
        {
            Console.CursorTop = 10;
            Console.CursorLeft = 15;
            Console.BackgroundColor = ConsoleColor.White;
            Console.ForegroundColor = ConsoleColor.Black;
            Console.WriteLine("                                                  ");
            Console.CursorLeft = 15;
            Console.WriteLine(" Enter your Netflix username and hit [Enter]:     ");
            Console.CursorLeft = 15;
            Console.WriteLine("                                                  ");
            Console.CursorLeft = 15;
            Console.WriteLine("                                                  ");
            Console.CursorLeft = 15;
            Console.WriteLine(" ──────────────────────────────────────────────── ");
            Console.CursorTop = 13;
            Console.CursorLeft = 16;
            Console.CursorVisible = true;

        }

        static void getPassword()
        {
            int top = Console.CursorTop;
            int left = Console.CursorLeft;
            Console.BackgroundColor = ConsoleColor.DarkGray;
            Console.Clear();
            drawUI();

            drawGetPassword();

            // Backspace Should Not Work
            string pass = string.Empty;
            ConsoleKeyInfo key;

            do
            {
                key = Console.ReadKey(true);

                // Backspace Should Not Work
                if (key.Key != ConsoleKey.Backspace && key.Key != ConsoleKey.Enter)
                {
                    pass += key.KeyChar;
                    Console.Write("*");
                }
                else
                {
                    if (key.Key == ConsoleKey.Backspace && pass.Length > 0)
                    {
                        pass = pass.Substring(0, (pass.Length - 1));
                        Console.Write("\b \b");
                    }
                }
            }
            // Stops Receving Keys Once Enter is Pressed
            while (key.Key != ConsoleKey.Enter);

            if (config.AppSettings.Settings["Password"] == null)
                config.AppSettings.Settings.Add(new KeyValueConfigurationElement("Password", ""));

            config.AppSettings.Settings["Password"].Value = pass;
            config.Save(ConfigurationSaveMode.Modified);
            ConfigurationManager.RefreshSection("appSettings");
            password = ConfigurationManager.AppSettings.Get("Password");

            Console.ResetColor();
            Console.Clear();
            drawUI();
        }

        public static void backupfolder(string path)
        {
            //string backupdestination = string.Empty;
            //if(backupinstance == string.Empty)
            //    backupinstance = path.Replace(rootpath, backuppath + DateTime.Now.ToString("yyyy-MM-dd_HHmmss") + @"\");

            //backupdestination = backupinstance + path;

            //backupdestination += @"\";
            //if (!Directory.Exists(backupdestination))
            //    Directory.CreateDirectory(backupdestination);

            foreach(string file in Directory.GetFiles(path))
            {
                FileInfo finfo = new FileInfo(file);
                //finfo.MoveTo(backupdestination + finfo.Name);
                finfo.Delete();
            }
            foreach (string folder in Directory.GetDirectories(path))
            {
                backupfolder(folder);
            }
        }

        static void drawGetPassword()
        {
            Console.CursorTop = 10;
            Console.CursorLeft = 15;
            Console.BackgroundColor = ConsoleColor.White;
            Console.ForegroundColor = ConsoleColor.Black;
            Console.WriteLine("                                                  ");
            Console.CursorLeft = 15;
            Console.WriteLine(" Enter your Netflix password and hit [Enter]:     ");
            Console.CursorLeft = 15;
            Console.WriteLine("                                                  ");
            Console.CursorLeft = 15;
            Console.WriteLine("                                                  ");
            Console.CursorLeft = 15;
            Console.WriteLine(" ──────────────────────────────────────────────── ");
            Console.CursorTop = 13;
            Console.CursorLeft = 16;
            Console.CursorVisible = true;
        }


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

        static void Login()
        {
            // setup the post data
            ASCIIEncoding encoding = new ASCIIEncoding();
            string postdata = "email=" + username + "&password=" + password + "&authURL=" + auth;
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

            Stream objStream = webreq.GetResponse().GetResponseStream();
            StreamReader reader = new StreamReader(objStream);

            List<String> lines = new List<string>();
        }


    }

}
