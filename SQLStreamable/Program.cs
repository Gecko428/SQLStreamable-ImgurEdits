using System;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using System.Data.SqlClient;
using System.Data;
using System.Threading;
using System.Diagnostics;
using System.Collections;
using System.Net.NetworkInformation;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Net.Http;
using System.Linq;

namespace SQLStreamable
{
    class Program
    {
        public static SqlConnection sql = new SqlConnection();


        public static ArrayList loaded = new ArrayList();
        public static ArrayList wrongformat = new ArrayList();
        public static ArrayList wrongconfigs = new ArrayList();
        public static ArrayList burl = new ArrayList();
        public static ArrayList bsource = new ArrayList();
        public static int totallinks = 0;
        public static int brokenlinks = 0;
        public static bool rewrite = false;

        public static string source = "";//@"Data Source=KyrarycPC;Initial Catalog=Streamable;Integrated Security=SSPI;";
        public static int minviews = -1;
        public static int maxviews = 10000;
        public static string selectnum = "";
        public static bool ensurenewview = false;
        public static int waittime = 0;
        static void Main(string[] args)
        {
            ToGfy();
            return;
            //The rest of this method was devoted to opening Streamables to get new views on them, not bothering to delete it
            try
            {
                if (!LoadConfig())
                {
                    return;
                }
                CheckRTs();
                CheckFiles();
                CheckLinks();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception encountered:  " + ex.Message + ", " + ex.StackTrace);
            }
            finally
            {
                if (sql.State == ConnectionState.Open)
                {
                    sql.Close();
                }
                Report();
            }
        }
        //This huge region is code used to open streamables to get them a new view and protect them from deletion, not bothering to delete it
        #region SQLStreamable
        public static void CheckRTs()
        {
            if (!File.Exists("RTLinks.txt"))
            {
                return;
            }

            string[] rts = File.ReadAllLines("RTLinks.txt");
            if (rts.Length == 0)
            {
                return;
            }

            IWebDriver driver = new ChromeDriver();
            for (int i = 0; i < rts.Length; i++)
            {
                driver.Url = rts[i];
                string filename = driver.Title;

                IList alllinks = driver.FindElements(By.TagName("a"));

                for (int c = 0; c < alllinks.Count; c++)
                {
                    IWebElement curlink = (IWebElement)alllinks[c];
                    string link = curlink.GetAttribute("href");

                    if (link == null || !link.Contains("streamable.com"))
                    {
                        continue;
                    }

                    //Get link from database
                    string query = "Select * FROM [Streamable].[dbo].[Links] " +
                        " where link like '%" + link + "%'";

                    var sqlProcedure = new SqlCommand
                    {
                        Connection = sql,
                        CommandType = CommandType.Text,
                        CommandText = query,
                        CommandTimeout = 10
                    };

                    var sqlAdapt = new SqlDataAdapter(sqlProcedure);
                    DataTable dt = new DataTable();
                    sqlAdapt.Fill(dt);

                    bool found = false;
                    for (int s = 0; s < dt.Rows.Count; s++)
                    {
                        string sqllink = dt.Rows[s]["link"].ToString();
                        if (sqllink.Equals(link))
                        {
                            string source = dt.Rows[s]["source"].ToString();
                            if (string.IsNullOrEmpty(source))
                            {//Double check that the source is stored
                                string sqlid = dt.Rows[s]["id"].ToString();
                                string update = "Update [Streamable].[dbo].[Links] set source = '" + filename + "' where id = " + sqlid;

                                var sqlUpdate = new SqlCommand
                                {
                                    Connection = sql,
                                    CommandType = CommandType.Text,
                                    CommandText = update,
                                    CommandTimeout = 10
                                };
                                sqlUpdate.ExecuteNonQuery();
                            }
                            found = true;
                            break;
                        }
                    }

                    if (!found)
                    {
                        //Wasn't found in sql, Insert it into the database
                        string insert = "insert into [Streamable].[dbo].[Links] ([link], [source], [views], [lastviewd]) values " +
                            "('" + link + "', '" + filename + "', '0', '" + (DateTime.Now.AddYears(-1)).ToShortDateString() + "')";

                        var sqlProcedureInsert = new SqlCommand
                        {
                            Connection = sql,
                            CommandType = CommandType.Text,
                            CommandText = insert,
                            CommandTimeout = 10
                        };
                        sqlProcedureInsert.ExecuteNonQuery();
                    }
                }
                loaded.Add(filename);
            }
            StreamWriter sw = new StreamWriter("RTLinks.txt");
            sw.WriteLine("");
            driver.Close();
        }
        public static bool LoadConfig()
        {
            if (!File.Exists("Config.txt"))
            {
                return false;
            }

            string[] config = File.ReadAllLines("Config.txt");

            for (int i = 0; i < config.Length; i++)
            {
                string type = config[i].Substring(0, 7);
                string set = config[i].Substring(9);
                switch (type.ToUpper())
                {
                    case "CONNECT":
                        {
                            source = set;
                            try
                            {
                                sql.ConnectionString = source;
                                sql.Open();
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine("Connection string not valid.  Ensure SQL Server is downloaded and the string is accurate.");
                                Console.WriteLine("My first time connection string should look like:  " + @"Data Source=KyrarycPC;Integrated Security=SSPI;");
                                Console.WriteLine("After setup it look like: " + @"Data Source=KyrarycPC;Integrated Security=SSPI;Initial Catalog=Streamable;");
                                Console.WriteLine("Replace 'KyrarycPC' with the name of your computer");

                                return false;
                            }
                            string testquery = "SELECT TOP 1 * FROM [Streamable].[dbo].[Links]";
                            try
                            {
                                var sqlProcedure = new SqlCommand
                                {
                                    Connection = sql,
                                    CommandType = CommandType.Text,
                                    CommandText = testquery,
                                    CommandTimeout = 10
                                };

                                var sqlAdapt = new SqlDataAdapter(sqlProcedure);
                                DataTable dt = new DataTable();
                                sqlAdapt.Fill(dt);

                            }
                            catch (Exception ex)
                            {
                                //Check if the STREAMABLE database exists
                                string createquery = "Create Database Streamable";
                                try
                                {
                                    var sqlProcedureDatabase = new SqlCommand
                                    {
                                        Connection = sql,
                                        CommandType = CommandType.Text,
                                        CommandText = createquery,
                                        CommandTimeout = 10
                                    };
                                    sqlProcedureDatabase.ExecuteNonQuery();
                                }
                                catch (Exception ex2)
                                {
                                    if (!ex2.Message.ToLower().Contains("already exist"))
                                    {
                                        Console.WriteLine("Unable to create database.  Please open SQL Server and create a Streamable database");
                                        return false;
                                    }
                                }
                                if (!source.Contains("Initial Catalog"))
                                {
                                    source = source + "Initial Catalog=Streamable;";
                                    sql.Close();
                                    sql.ConnectionString = source;
                                    sql.Open();
                                    rewrite = true;
                                }
                                //Attempt to create the Link database
                                createquery = "CREATE TABLE[dbo].[Links](" +
                                    " [id][int] IDENTITY(1, 1) NOT NULL, [link] [text] NULL, [source] [text] NULL, [views] [int] NULL, [lastviewd] [date] NULL," +
                                    " CONSTRAINT[PK_Links] PRIMARY KEY CLUSTERED ([id] ASC )WITH(PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON[PRIMARY])";

                                try
                                {
                                    var sqlProcedureLink = new SqlCommand
                                    {
                                        Connection = sql,
                                        CommandType = CommandType.Text,
                                        CommandText = createquery,
                                        CommandTimeout = 10
                                    };
                                    sqlProcedureLink.ExecuteNonQuery();
                                }
                                catch (Exception ex2)
                                {
                                    if (!ex2.Message.ToLower().Contains("already an object named"))
                                    {
                                        Console.WriteLine("Unable to add the links database.  Please open SQL Server and run the following query");
                                        Console.WriteLine(createquery);
                                        return false;
                                    }
                                }

                                //        string testinsert = "insert into [Test].[dbo].[Links] ([link], [source], [views], [lastviewd]) values " +
                                //"('Test', 'TEST1', '0', '" + (DateTime.Now.AddYears(-1)).ToShortDateString() + "')";
                                //        var sqlProcedureTest = new SqlCommand
                                //        {
                                //            Connection = sql,
                                //            CommandType = CommandType.Text,
                                //            CommandText = testinsert,
                                //            CommandTimeout = 10
                                //        };
                                //        sqlProcedureTest.ExecuteNonQuery();
                                //        sqlProcedureTest.ExecuteNonQuery();
                                //        sqlProcedureTest.ExecuteNonQuery();

                                //Attempt to create the broken database
                                createquery = "CREATE TABLE[dbo].[Broken](" +
                                    "[id][int] IDENTITY(1, 1) NOT NULL, [link] [text] NULL, [source] [text] NULL, [timestamp] [datetime] NULL," +
                                    "CONSTRAINT[PK_Broken] PRIMARY KEY CLUSTERED ([id] ASC)WITH(PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON[PRIMARY])";

                                try
                                {
                                    var sqlProcedureBroken = new SqlCommand
                                    {
                                        Connection = sql,
                                        CommandType = CommandType.Text,
                                        CommandText = createquery,
                                        CommandTimeout = 10
                                    };
                                    sqlProcedureBroken.ExecuteNonQuery();
                                }
                                catch (Exception ex2)
                                {
                                    if (!ex2.Message.ToLower().Contains("already an object named"))
                                    {
                                        Console.WriteLine("Unable to add the broken links database.  Please open SQL Server and run the following query");
                                        Console.WriteLine(createquery);
                                        return false;
                                    }
                                }
                            }
                            break;
                        }
                    case "MINVIEW":
                        {
                            int view;
                            if (int.TryParse(set, out view))
                            {
                                minviews = view;
                            }
                            break;
                        }
                    case "MAXVIEW":
                        {
                            int view;
                            if (int.TryParse(set, out view))
                            {
                                maxviews = view;
                            }
                            break;
                        }
                    case "SELECT#":
                        {
                            int view;
                            if (!int.TryParse(set, out view))
                            {
                                selectnum = " * ";
                            }
                            else
                            {
                                selectnum = " Top " + view + " * ";
                            }
                            break;
                        }
                    case "WAITMIL":
                        {
                            int num;
                            if (!int.TryParse(set, out num))
                            {
                                waittime = 1500;
                            }
                            else
                            {
                                waittime = num;
                            }
                            break;
                        }
                    case "NEWVIEW":
                        {
                            if (set.ToUpper().Equals("TRUE") || set.ToUpper().Equals("YES"))
                            {
                                ensurenewview = true;
                            }
                            else
                            {
                                ensurenewview = false;
                            }
                            break;
                        }
                    default:
                        {
                            wrongconfigs.Add(type);
                            break;
                        }
                }
            }
            return true;
        }

        public static void Report()
        {

            if (wrongconfigs.Count > 0)
            {
                Console.WriteLine("~~~~~~~~~~~~~~~~~~~~~~~~Config commands not recognized~~~~~~~~~~~~~~~~~~~~~~~~");
                for (int i = 0; i < wrongconfigs.Count; i++)
                {
                    Console.WriteLine(wrongconfigs[i].ToString());
                }
                Console.WriteLine("~~~~~~~~~~~~~~~~~~~~~~~~Config commands not recognized~~~~~~~~~~~~~~~~~~~~~~~~");
            }
            if (loaded.Count > 0)
            {
                Console.WriteLine("~~~~~~~~~~~~~~~~~~~~~~~~Loaded Files~~~~~~~~~~~~~~~~~~~");
                for (int i = 0; i < loaded.Count; i++)
                {
                    Console.WriteLine(loaded[i].ToString());
                }
                Console.WriteLine("~~~~~~~~~~~~~~~~~~~~~~~~Loaded Files~~~~~~~~~~~~~~~~~~~");
            }
            if (wrongformat.Count > 0)
            {
                Console.WriteLine("~~~~~~~~~~~~~~~~~~~~~~~~Improperly formatted files - .txt ONLY~~~~~~~~~~~~~~~~~~~");
                for (int i = 0; i < wrongformat.Count; i++)
                {
                    Console.WriteLine(wrongformat[i].ToString());
                }
                Console.WriteLine("~~~~~~~~~~~~~~~~~~~~~~~~Improperly formatted files - .txt ONLY~~~~~~~~~~~~~~~~~~~");
            }
            if (burl.Count > 0)
            {
                Console.WriteLine("~~~~~~~~~~~~~~~~~~~~~~~~Broken Links~~~~~~~~~~~~~~~~~~~");
                for (int i = 0; i < burl.Count; i++)
                {
                    Console.WriteLine(burl[i].ToString() + " - " + bsource[i].ToString());
                }
                Console.WriteLine("~~~~~~~~~~~~~~~~~~~~~~~~Broken Links~~~~~~~~~~~~~~~~~~~");
            }
            if (rewrite)
            {
                Console.WriteLine("Rewriting config file");
                StreamWriter sw = new StreamWriter("config.txt");
                sw.WriteLine("CONNECT: " + source);
                sw.WriteLine("MINVIEW: " + minviews);
                sw.WriteLine("MAXVIEW: " + maxviews);
                string tempselect = selectnum.Replace("top", "").Replace("*", "").Trim();
                int intselect;
                if (int.TryParse(tempselect, out intselect))
                {
                    sw.WriteLine("SELECT#: " + source);
                }
                else
                {
                    sw.WriteLine("SELECT#: *");
                }
                sw.WriteLine("WAITMIL: " + waittime);
                sw.WriteLine("NEWVIEW: " + ensurenewview);

                sw.Close();
            }
            Console.WriteLine("Done.  " + totallinks + " links scanned, " + brokenlinks + " broken links found.  Close whenever");
        }
        public static void CheckFiles()
        {
            string curloc = Directory.GetCurrentDirectory();
            string[] rts = Directory.GetFiles(curloc + "\\RTs\\");
            //Console.WriteLine(rts.Length);

            //26
            for (int i = 0; i < rts.Length; i++)
            {
                Console.WriteLine("Checking " + rts[i]);
                string filename = rts[i].Substring(curloc.Length + 5);
                string ext = filename.Substring(filename.Length - 4);
                if (!string.Equals(ext, ".txt"))
                {
                    Console.WriteLine("Only .txt files allowed - " + rts[i]);
                    wrongformat.Add(filename + ext);
                    continue;
                }
                filename = filename.Substring(0, filename.Length - 4);

                string curfile = File.ReadAllText(rts[i]);
                while (curfile.IndexOf("streamable.com") != -1)
                {

                    int pos = curfile.IndexOf("streamable.com");
                    curfile = curfile.Substring(pos);

                    //determine end of streamable link
                    int space = curfile.IndexOf(" ");
                    int par = curfile.IndexOf(")");
                    int quote = curfile.IndexOf("\"");
                    int line = curfile.IndexOf("\r");

                    if (space == -1)
                    {
                        space = int.MaxValue;
                    }
                    if (par == -1)
                    {
                        par = int.MaxValue;
                    }
                    if (quote == -1)
                    {
                        quote = int.MaxValue;
                    }
                    if (line == -1)
                    {
                        line = int.MaxValue;
                    }

                    if (space > par)
                    {
                        space = par;
                    }
                    if (space > quote)
                    {
                        space = quote;
                    }
                    if (space > line)
                    {
                        space = line;
                    }

                    if (space == int.MaxValue)
                    {
                        curfile = "";
                        continue;
                    }

                    string link = "https://" + curfile.Substring(0, space);
                    curfile = curfile.Substring(space);
                    //Console.WriteLine(link);

                    //Get link from database
                    string query = "Select * FROM [Streamable].[dbo].[Links] " +
                        " where link like '%" + link + "%'";

                    var sqlProcedure = new SqlCommand
                    {
                        Connection = sql,
                        CommandType = CommandType.Text,
                        CommandText = query,
                        CommandTimeout = 10
                    };

                    var sqlAdapt = new SqlDataAdapter(sqlProcedure);
                    DataTable dt = new DataTable();
                    sqlAdapt.Fill(dt);

                    bool found = false;
                    for (int s = 0; s < dt.Rows.Count; s++)
                    {
                        string sqllink = dt.Rows[s]["link"].ToString();
                        if (sqllink.Equals(link))
                        {
                            string source = dt.Rows[s]["source"].ToString();
                            if (string.IsNullOrEmpty(source))
                            {//Double check that the source is stored
                                string sqlid = dt.Rows[s]["id"].ToString();
                                string update = "Update [Streamable].[dbo].[Links] set source = '" + filename + "' where id = " + sqlid;

                                var sqlUpdate = new SqlCommand
                                {
                                    Connection = sql,
                                    CommandType = CommandType.Text,
                                    CommandText = update,
                                    CommandTimeout = 10
                                };
                                sqlUpdate.ExecuteNonQuery();
                            }
                            found = true;
                            break;
                        }
                    }

                    if (found)
                    {
                        continue;
                    }

                    //Wasn't found in sql, Insert it into the database
                    string insert = "insert into [Streamable].[dbo].[Links] ([link], [source], [views], [lastviewd]) values " +
                        "('" + link + "', '" + filename + "', '0', '" + (DateTime.Now.AddYears(-1)).ToShortDateString() + "')";

                    var sqlProcedureInsert = new SqlCommand
                    {
                        Connection = sql,
                        CommandType = CommandType.Text,
                        CommandText = insert,
                        CommandTimeout = 10
                    };
                    sqlProcedureInsert.ExecuteNonQuery();
                }

                loaded.Add(filename + ext);
                File.Delete(rts[i]);
            }
        }
        public static void CheckLinks()
        {
            string query = "SELECT " + selectnum + " FROM[Streamable].[dbo].[Links] where views <= " + maxviews +
                " and views >= " + minviews + " order by lastviewd, views desc";

            var sqlProcedure = new SqlCommand
            {
                Connection = sql,
                CommandType = CommandType.Text,
                CommandText = query,
                CommandTimeout = 10
            };

            var sqlAdapt = new SqlDataAdapter(sqlProcedure);
            DataTable dt = new DataTable();
            sqlAdapt.Fill(dt);

            IWebDriver driver = new ChromeDriver();

            for (int i = 0; i < dt.Rows.Count; i++)
            {
                DataRow curRow = dt.Rows[i];
                string curURL = curRow["link"].ToString();
                string curID = curRow["id"].ToString();

                driver.Url = curURL;
                totallinks++;
                try
                {
                    driver.FindElement(By.Id("play-button")).Click();
                }
                catch (Exception ex)
                {
                    try
                    {
                        IWebElement videoplayer = driver.FindElement(By.Id("video-player-tag"));
                    }
                    catch (Exception ex3)
                    {
                        try
                        {
                            IWebElement notfound = driver.FindElement(By.XPath("//*[contains(., 'Oops')]"));
                            Console.WriteLine("Broken video detected - " + curURL);
                            burl.Add(curURL);
                            bsource.Add(curRow["source"].ToString());
                            brokenlinks++;
                            string insert = "insert into [Streamable].[dbo].[Broken] ([link], [source], [timestamp]) values " +
                        "('" + curURL + "', '" + curRow["source"].ToString() + "', '" + DateTime.Now.ToShortDateString() + "')";

                            var sqlProcedureInsert = new SqlCommand
                            {
                                Connection = sql,
                                CommandType = CommandType.Text,
                                CommandText = insert,
                                CommandTimeout = 10
                            };
                            sqlProcedureInsert.ExecuteNonQuery();

                            string delete = "Delete from [Streamable].[dbo].[Links] where id =" + curID;
                            var sqlProcedureDelete = new SqlCommand
                            {
                                Connection = sql,
                                CommandType = CommandType.Text,
                                CommandText = delete,
                                CommandTimeout = 10
                            };
                            sqlProcedureDelete.ExecuteNonQuery();
                        }
                        catch (Exception ex2)
                        {
                            //Not broken, just being weird.  continue on without trying anything
                        }
                        continue;
                    }
                }
                bool newview = false;

                IWebElement views = driver.FindElement(By.Id("visits"));
                string strviews = views.Text.Replace("views", "").Replace(",", "").Trim();
                int numviews = int.Parse(strviews);

                Stopwatch sw = new Stopwatch();
                sw.Start();
                while (!newview && sw.Elapsed.TotalSeconds < 30 && ensurenewview && numviews <= 100)
                {
                    ChromeOptions opt = new ChromeOptions();
                    opt.AddArgument("--autoplay-policy=no-user-gesture-required");
                    IWebDriver sdriver = new ChromeDriver(opt);
                    sdriver.Url = curURL;

                    IWebElement views2 = sdriver.FindElement(By.Id("visits"));
                    string strviews2 = views2.Text.Replace("views", "").Trim();
                    int numviews2 = int.Parse(strviews2);

                    if (numviews2 > numviews)
                    {
                        newview = true;
                        numviews = numviews2;
                    }
                    else
                    {
                        Thread.Sleep(waittime);
                    }
                    sdriver.Close();
                }

                string updatequery = "Update [Streamable].[dbo].[Links] set views = " + numviews + ", lastviewd = '" +
                           DateTime.Now.ToShortDateString() + "' where id = " + curID;

                //Console.WriteLine(updatequery);

                sqlProcedure = new SqlCommand
                {
                    Connection = sql,
                    CommandType = CommandType.Text,
                    CommandText = updatequery,
                    CommandTimeout = 10
                };
                sqlProcedure.ExecuteNonQuery();

                Thread.Sleep(waittime);
            }
            driver.Close();
            return;
        }
        #endregion

        //This region is code used to transfer streamables/gfycats to reddit
        #region ToReddit

        public static IWebDriver driver2;
        public static ArrayList brokenStreams;
        public static ArrayList brokenSources;
        public static ArrayList gfyFails;
        public static ArrayList redditFails;
        public static ArrayList tooShort;
        public static string downloadDirectory = @"D:\Test\Download\";
        public static string threadsDirectory = @"D:\Test\Download\";
        public static string rtLinksDoc = @"D:\Test\thread list.txt";
        public static string videoFolder = @"D:\Gif Archive\";
        public static string redditUsername = "doctorgecko";
        public static string redditPassword = "doctorgeckospassword";
        public static string sqlConnection = "Data Source=localhost\\SQLEXPRESS;Integrated Security=SSPI;";
        public static string imgurEmail = "doctorgecko@doctorgecko.com";
        public static string imgurPassword = "doctorgeckoing";
        public static bool checkImgurs = true;
        public static bool getFromFolder = true;
        public static bool uploadVideos = true;
        public static bool uploadImgur = true;
        public static int maxImgurChecks = int.MaxValue;
        public static bool newImgur = true;
        public static string imgurURL = "https://imgur.com/upload";

        public static ArrayList shortSource;
        public static ArrayList redgifs;
        public static ArrayList redSources;
        public static ArrayList ImgurMissing;
        public static ArrayList fileMissing;
        public static ArrayList ImgurSource;
        public static ArrayList emptyGallery;
        public static ArrayList emptySource;
        public static ArrayList imgurList;
        public static ArrayList imgurFails;

        public static void ToGfy()
        {
            if (!OpenSQL())
            {
                return;
            }

            brokenStreams = new ArrayList();
            brokenSources = new ArrayList();
            gfyFails = new ArrayList();
            redditFails = new ArrayList();
            tooShort = new ArrayList();
            shortSource = new ArrayList();
            redgifs = new ArrayList();
            redSources = new ArrayList();
            ImgurSource = new ArrayList();
            ImgurMissing = new ArrayList();
            emptyGallery = new ArrayList();
            emptySource = new ArrayList();
            fileMissing = new ArrayList();
            imgurList = new ArrayList();
            ImgurMissing = new ArrayList();
            brokenlinks = 0;

            ChromeOptions opts = new ChromeOptions();
            opts.AddUserProfilePreference("download.default_directory", downloadDirectory);
            opts.AddArgument("--disable-notifications");
            opts.UnhandledPromptBehavior = UnhandledPromptBehavior.Dismiss;
            driver2 = new ChromeDriver(opts);

            RedditLogin();

            if (uploadImgur)
            {
                ImgurLogin();
            }

            string[] rtLinks = File.ReadAllLines(rtLinksDoc);

            for (int i = 0; i < rtLinks.Length; i++)
            {
                GetReddit(rtLinks[i]);
            }

            Console.WriteLine("***************************************");
            Console.WriteLine("Finished scanning threads.");

            if (checkImgurs)
            {
                CheckImgur();

                Console.WriteLine("***************************************");
                Console.WriteLine("Finished scanning Imgur Links.");
            }

            Console.WriteLine("***************************************");
            Console.WriteLine("Threads scanned:");
            for (int i = 0; i < loaded.Count; i++)
            {
                Console.WriteLine(loaded[i].ToString());
            }

            if (tooShort.Count > 0)
            {
                Console.WriteLine("***************************************");
                Console.WriteLine("The following videos were too short to upload to Reddit");
                for (int i = 0; i < tooShort.Count; i++)
                {
                    Console.WriteLine(tooShort[i].ToString() + " - " + shortSource[i].ToString());
                }
            }

            if (gfyFails.Count > 0)
            {
                Console.WriteLine("***************************************");
                Console.WriteLine("The following videos were unabled to be downloaded");
                for (int i = 0; i < gfyFails.Count; i++)
                {
                    Console.WriteLine(gfyFails[i].ToString());
                }
            }

            if (redditFails.Count > 0)
            {
                Console.WriteLine("***************************************");
                Console.WriteLine("The following videos were unabled to be uploaded to Reddit");
                for (int i = 0; i < redditFails.Count; i++)
                {
                    Console.WriteLine(redditFails[i].ToString());
                }
            }

            if (redgifs.Count > 0)
            {
                Console.WriteLine("***************************************");
                Console.WriteLine("The following videos were Redgif videos, unable to be downloaded.");
                for (int i = 0; i < redgifs.Count; i++)
                {
                    Console.WriteLine(redgifs[i].ToString() + " - " + redSources[i].ToString());
                }
            }

            Console.WriteLine("***************************************");
            Console.WriteLine("Broken Gfycat/Streamable links found: " + brokenlinks);
            for (int i = 0; i < brokenStreams.Count; i++)
            {
                Console.WriteLine(brokenStreams[i].ToString() + " - " + brokenSources[i].ToString());
            }
            
            if (ImgurMissing.Count > 0)
            {
                Console.WriteLine("***************************************");
                Console.WriteLine("Broken Imgur links found: " + ImgurMissing.Count);
                for (int i = 0; i < ImgurMissing.Count; i++)
                {
                    Console.WriteLine(ImgurMissing[i].ToString() + " - " + ImgurSource[i].ToString());
                }
            }

            if (emptyGallery.Count > 0)
            {
                Console.WriteLine("***************************************");
                Console.WriteLine("Possible Empty Imgur Galleries found: " + emptyGallery.Count);
                for (int i = 0; i < emptyGallery.Count; i++)
                {
                    Console.WriteLine(emptyGallery[i].ToString() + " - " + emptySource[i].ToString());
                }
            }

            if (fileMissing.Count > 0)
            {
                Console.WriteLine("***************************************");
                Console.WriteLine("Videos not found in folder: " + fileMissing.Count);
                for (int i = 0; i < fileMissing.Count; i++)
                {
                    Console.WriteLine(fileMissing[i].ToString());
                }
            }

            if (imgurFails.Count > 0)
            {
                Console.WriteLine("***************************************");
                Console.WriteLine("Videos failed to upload to imgur: " + imgurFails.Count);
                for (int i = 0; i < imgurFails.Count; i++)
                {
                    Console.WriteLine(imgurFails[i].ToString());
                }
            }
        }
        public static void DownloadGfy(string url, string source)
        {
            driver2.Url = url;

            if (driver2.Url.Contains("redgifs.com/"))
            {
                Console.WriteLine("Redgif detected - " + url);
                if (!redgifs.Contains(url))
                {
                    redgifs.Add(url);
                    redSources.Add(source);
                }
                return;
            }

            try
            {
                IWebElement notfound = driver2.FindElement(By.XPath("//h1[contains(text(), 'Page not found')]"));
                if (!brokenStreams.Contains(url))
                {
                    Console.WriteLine("Broken video detected - " + url);
                    brokenStreams.Add(url);
                    brokenSources.Add(source);
                    brokenlinks++;
                }
                string query = "Delete from  [RedditVids].[dbo].[Links]" +
                           "' where gfycat like '" + url + "'";

                var sqlProcedure = new SqlCommand
                {
                    Connection = sql,
                    CommandType = CommandType.Text,
                    CommandText = query,
                    CommandTimeout = 10
                };
                sqlProcedure.ExecuteNonQuery();

                return;
            }
            catch (Exception ex)
            {
                //an exception here should mean that the gfycat isn't broken
            }
            System.Collections.ObjectModel.ReadOnlyCollection<IWebElement> autoEle = driver2.FindElements(By.XPath("//input[contains(@type, 'checkbox')]"));
            if (autoEle.Count > 0)
            {
                string autoPlay = autoEle[0].GetAttribute("checked");
                if (autoPlay == "true")
                {
                    autoEle[0].Click();
                }
            }
            System.Collections.ObjectModel.ReadOnlyCollection<IWebElement> durEles = driver2.FindElements(By.XPath("//meta[contains(@property, 'og:video:duration')]"));
            if (durEles.Count > 0)
            {
                string dur = durEles[0].GetAttribute("content");
                double dblDur;
                if (double.TryParse(dur, out dblDur))
                {
                    if (dblDur <= 2)
                    {
                        if (!tooShort.Contains(url))
                        {
                            Console.WriteLine("Video too short to upload to reddit: " + url + ", length " + dblDur);
                            tooShort.Add(url);
                            shortSource.Add(source);
                        }
                        string query = "Delete from  [RedditVids].[dbo].[Links]" +
                           " where gfycat like '" + url + "'";

                        var sqlProcedure = new SqlCommand
                        {
                            Connection = sql,
                            CommandType = CommandType.Text,
                            CommandText = query,
                            CommandTimeout = 10
                        };
                        sqlProcedure.ExecuteNonQuery();

                        return;
                    }
                }
            }

            System.Collections.ObjectModel.ReadOnlyCollection<IWebElement> linkEles = driver2.FindElements(By.XPath("//source"));//[contains(src, '.mp4')]"));
            string thumbLink = "";
            string giantLink = "";
            for (int i = 0; i < linkEles.Count; i++)
            {
                string curLink = linkEles[i].GetAttribute("src");
                if (curLink.Contains("giant.gfycat.com"))
                {
                    giantLink = curLink;
                    break;
                }
                else if (curLink.Contains("thumbs.gfycat.com"))
                {//backup link
                    thumbLink = curLink;
                }
            }

            if (giantLink == "")
            {
                giantLink = thumbLink;
            }

            if (giantLink == "")
            {
                //can't download, for whatever reason
                if (!gfyFails.Contains(url))
                {
                    gfyFails.Add(url);
                }
                return;
            }
            IWebElement gfyTitle = driver2.FindElement(By.XPath("//h1"));
            string title = gfyTitle.Text.Replace(" GIF", "");
            string name = url.Substring(url.IndexOf(".com/") + 5) + ".mp4";

            using (var client = new HttpClient())
            {
                using (var s = client.GetStreamAsync(giantLink))
                {
                    using (var fs = new FileStream(downloadDirectory + name, FileMode.OpenOrCreate))
                    {
                        s.Result.CopyTo(fs);
                    }
                }
            }

            if (uploadImgur)
            {
                UploadImgur(url, name, "streamable");
            }
            else
            {
                UploadReddit(url, title, "gfycat", name);
            }
            
        }
        public static bool OpenSQL()
        {
            try
            {
                sql.ConnectionString = sqlConnection;
                sql.Open();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Connection string not valid.  Ensure SQL Server is downloaded and the string is accurate.");
                Console.WriteLine("It should look like: " + @"Data Source=KyrarycPC;Integrated Security=SSPI;");
                Console.WriteLine("Replace 'KyrarycPC' with the name of your computer");

                return false;
            }
            return CreateDatabase();
        }
        public static void DownloadFromStreamable(string url, string source)
        {
            driver2.Url = url;

            try
            {
                IWebElement notfound = driver2.FindElement(By.XPath("//p[contains(text(), \"We couldn't find your video.\")]"));
                if (!brokenStreams.Contains(url))
                {
                    Console.WriteLine("Broken video detected - " + url);
                    brokenStreams.Add(url);
                    brokenSources.Add(source);
                    brokenlinks++;
                }
                string query = "Delete from  [RedditVids].[dbo].[Links]" +
                           "' where streamable like '" + url + "'";

                var sqlProcedure = new SqlCommand
                {
                    Connection = sql,
                    CommandType = CommandType.Text,
                    CommandText = query,
                    CommandTimeout = 10
                };
                sqlProcedure.ExecuteNonQuery();
                return;
            }
            catch (Exception ex)
            {
                //an exception here should mean that the streamable isn't broken
            }

            IWebElement scriptEle = driver2.FindElement(By.XPath("//script[@data-duration]"));
            string vidLength = scriptEle.GetAttribute("data-duration");
            string title = driver2.FindElement(By.Id("title")).Text;

            System.Collections.ObjectModel.ReadOnlyCollection<IWebElement> downloadLinks = driver2.FindElements(By.Id("download"));
            string downloadUrl = downloadLinks[0].GetAttribute("href");
            
            string newTitle = url.Substring(url.IndexOf(".com/") + 5);
            string name = newTitle + ".mp4";
            newTitle += title;

            double dblLength = -1;
            if (double.TryParse(vidLength, out dblLength))
            {
                if (dblLength <= 2)
                {
                    if (!tooShort.Contains(url))
                    {
                        Console.WriteLine("Video too short to upload to reddit: " + url + ", length " + dblLength);
                        tooShort.Add(url);
                        shortSource.Add(source);
                    }
                    string query = "Delete from  [RedditVids].[dbo].[Links]" +
                       " where streamable like '" + url + "'";

                    var sqlProcedure = new SqlCommand
                    {
                        Connection = sql,
                        CommandType = CommandType.Text,
                        CommandText = query,
                        CommandTimeout = 10
                    };
                    sqlProcedure.ExecuteNonQuery();

                    return;
                }
            }

            using (var client = new HttpClient())
            {
                using (var s = client.GetStreamAsync(downloadUrl))
                {
                    using (var fs = new FileStream(downloadDirectory + name, FileMode.OpenOrCreate))
                    {
                        s.Result.CopyTo(fs);
                    }
                }
            }
            if (uploadImgur)
            {
                UploadImgur(url, name, "streamable");
            }
            else
            {
                UploadReddit(url, title, "streamable", name);
            }
            
        }
        public static void UploadReddit(string url, string title, string type, string vidName)
        {
            try
            {
                //string currentUpload = Directory.GetFiles(@"C:\Users\Kyr\Downloads\Longer\")[0].Substring(34).Replace(".mp4", "");
                driver2.Url = "https://www.reddit.com/r/RTvideos/submit";// "https://www.reddit.com/r/RTvideos/submit";
                IWebElement titleEle = driver2.FindElement(By.XPath("//textarea[@name='title']"));
                //titleEle.SendKeys(currentUpload);
                titleEle.SendKeys(title);

                ClickElementXPath("//div/label[contains(text(), 'Choose File')]");

                System.Collections.ObjectModel.ReadOnlyCollection<IWebElement> progressEles = driver2.FindElements(By.Id("media-progress-bar"));
                while (progressEles.Count == 0)
                {//This will wait until you actually upload the video yourself
                    progressEles = driver2.FindElements(By.Id("media-progress-bar"));
                }

                IWebElement progressEle = driver2.FindElement(By.Id("media-progress-bar"));
                IWebElement subButton = driver2.FindElement(By.XPath("//div/button[contains(text(), 'submit')]"));
                string dis = subButton.GetAttribute("disabled");

                while (progressEle.Text != "100%" || dis == "true")
                {
                    System.Collections.ObjectModel.ReadOnlyCollection<IWebElement> errorEle = driver2.FindElements(By.XPath("//div[contains(text(), 'something went wrong')]"));
                    if (errorEle.Count > 0)
                    {
                        //something went wrong, redo it
                        int adfasdf = 0;
                    }
                    progressEle = driver2.FindElement(By.Id("media-progress-bar"));
                    subButton = driver2.FindElement(By.XPath("//div/button[contains(text(), 'submit')]"));
                    dis = subButton.GetAttribute("disabled");
                }

                ClickElementXPath("//div/button[contains(text(), 'submit')]");

                //System.Collections.ObjectModel.ReadOnlyCollection<IWebElement> shortlink = driver2.FindElements(By.XPath("//a[contains(text(), '" + currentUpload + "')]"));
                System.Collections.ObjectModel.ReadOnlyCollection<IWebElement> shortlink = driver2.FindElements(By.XPath("//a[contains(@href, 'https://v.redd.it/')]"));


                while (shortlink.Count == 0)
                {
                    try
                    {
                        subButton = driver2.FindElement(By.XPath("//div/button[contains(text(), 'submit')]"));
                        dis = subButton.GetAttribute("disabled");
                        if (dis != "true")
                        {
                            ClickElementXPath("//div/button[contains(text(), 'submit')]");
                        }
                    }
                    catch (Exception ex4)
                    {

                    }
                    //shortlink = driver2.FindElements(By.XPath("//a[contains(text(), '" + currentUpload + "')]"));
                    shortlink = driver2.FindElements(By.XPath("//a[contains(@href, 'https://v.redd.it/')]"));
                }
                string vAdd = shortlink[0].GetAttribute("href");

                //string query = "Update [RedditVids].[dbo].[Links] set [reddit] = " + vAdd + ", views = 0" +
                //           "' where streamable like 'https://streamable.com/" + currentUpload.Substring(0, currentUpload.IndexOf(" ")).Trim() + "'";
                string query = "Update [RedditVids].[dbo].[Links] set [reddit] = '" + vAdd +
                           "' where " + type +  " like '" + url + "'";

                var sqlProcedure = new SqlCommand
                {
                    Connection = sql,
                    CommandType = CommandType.Text,
                    CommandText = query,
                    CommandTimeout = 10
                };
                sqlProcedure.ExecuteNonQuery();

                File.Delete(downloadDirectory + vidName);
            }
            catch (Exception ex)
            {
                string query = "Select * from [RedditVids].[dbo].[Links] where " + type + " like '" + url + "'";

                var sqlProcedure = new SqlCommand
                {
                    Connection = sql,
                    CommandType = CommandType.Text,
                    CommandText = query,
                    CommandTimeout = 10
                };

                var sqlAdapt = new SqlDataAdapter(sqlProcedure);
                DataTable dt = new DataTable();
                sqlAdapt.Fill(dt);

                if (dt.Rows.Count == 0 || string.IsNullOrEmpty(dt.Rows[0]["reddit"].ToString()))
                {
                    if (!redditFails.Contains(url))
                    {
                        redditFails.Add(url);
                    }
                }
            }
        }

        public static void UploadImgur(string url, string vidname, string type)
        {
            
            try
            {
            driver2.Url = imgurURL;
            if (newImgur)
            {
                bool cont = false;
                while (!cont)
                {

                    string currentURL = driver2.Url;

                    if (currentURL != imgurURL)
                    {
                        imgurURL = currentURL;
                        cont = true;
                        newImgur = false;
                    }
                    
                    System.Collections.ObjectModel.ReadOnlyCollection<IWebElement>  errors = driver2.FindElements(By.ClassName("UploadError"));
                    if (errors.Count >0)
                    {
                        Console.WriteLine("Hourly Upload Limit Reached. Please wait and then press any key to continue");
                        Console.ReadKey();
                    }
                    
                }
            }
            else if (!newImgur)
            {
                Thread.Sleep(5000);
                IWebElement submit = driver2.FindElement(By.XPath("//button[contains(@title, 'Add image')]"));
                submit.Click();
            }
            Thread.Sleep(5000);

            bool newlink = false;

            while (!newlink)
            {
                System.Collections.ObjectModel.ReadOnlyCollection<IWebElement> shortlink = driver2.FindElements(By.XPath("//source[contains(@src, 'https://i.imgur.com/')]"));

                if (shortlink != null)
                {
                    IWebElement mainlink = shortlink.Last();
                    string mainURL = mainlink.GetAttribute("src");

                    if (!imgurList.Contains(mainURL))
                    {
                        imgurList.Add(mainURL);
                        newlink = true;

                        string query = "Update [RedditVids].[dbo].[Links] set [reddit] = '" + mainURL +
                               "' where " + type + " like '" + url + "'";

                        var sqlProcedure = new SqlCommand
                        {
                            Connection = sql,
                            CommandType = CommandType.Text,
                            CommandText = query,
                            CommandTimeout = 10
                        };
                        sqlProcedure.ExecuteNonQuery();

                        File.Delete(downloadDirectory + vidname);
                    }
                }
                System.Collections.ObjectModel.ReadOnlyCollection<IWebElement> errors = driver2.FindElements(By.ClassName("UploadError"));
                if (errors.Count > 0)
                {
                    Console.WriteLine("Hourly Upload Limit Reached. Please wait and then press any key to continue");
                    Console.ReadKey();
                    Thread.Sleep(5000);
                    IWebElement submit = driver2.FindElement(By.XPath("//button[contains(@title, 'Add image')]"));
                    submit.Click();

                }

            }

            }
            catch
            {
                string query = "Select * from [RedditVids].[dbo].[Links] where " + type + " like '" + url + "'";

                var sqlProcedure = new SqlCommand
                {
                    Connection = sql,
                    CommandType = CommandType.Text,
                    CommandText = query,
                    CommandTimeout = 10
                };

                var sqlAdapt = new SqlDataAdapter(sqlProcedure);
                DataTable dt = new DataTable();
                sqlAdapt.Fill(dt);

                if (dt.Rows.Count == 0 || string.IsNullOrEmpty(dt.Rows[0]["reddit"].ToString()))
                {
                    if (!imgurFails.Contains(url))
                    {
                        imgurFails.Add(url);
                    }
                }
            }
        }
        public static void UploadGfy()
        {
            try
            {
                while (Directory.GetFiles(@"C:\Users\Kyr\Downloads\Upload\").Length > 0)
                {
                    driver2.Url = "https://gfycat.com/login";
                    SetTextName("username", "kyraryc");
                    SetTextName("password", "kyrstotallyrealgfycatpassword");
                    ClickElementXPath("//button[contains(text(),'Log In')]");
                    Thread.Sleep(5000);
                    driver2.Url = ("https://gfycat.com/upload");
                    ClickElementXPath("//span[@data-tooltip='Make a video (with sound)']/button");
                    IWebElement divEle = driver2.FindElement(By.ClassName("file-input"));
                    divEle.Click();
                    Stopwatch sw = new Stopwatch();
                    sw.Start();
                    System.Collections.ObjectModel.ReadOnlyCollection<IWebElement> encod;
                    do
                    {//Encoding...
                        encod = driver2.FindElements(By.XPath("//div[contains(text(), 'Encoding...')]"));
                        if (sw.Elapsed.TotalSeconds > 600)
                        {
                            int blasdf = 0;
                        }
                    } while (encod.Count > 0);

                    System.Collections.ObjectModel.ReadOnlyCollection<IWebElement> vids = driver2.FindElements(By.XPath("//div[contains(@class, 'upload-complete')]"));
                    for (int i = 0; i < vids.Count; i++)
                    {
                        IWebElement titleEle = vids[i].FindElement(By.XPath(".//input[contains(@placeholder, 'Add Title')]"));
                        string strTitle = titleEle.GetAttribute("value");
                        IWebElement urlEle = vids[i].FindElement(By.ClassName("copy-input-text"));
                        string strUrl = urlEle.GetAttribute("value");

                        string prefix = strTitle.Substring(0, strTitle.IndexOf(" ")).Trim();

                        string orgUrl = "https://streamable.com/" + prefix;
                        string query = "Update [RedditVids].[dbo].[Links] set [gfycat] = " + strUrl + ", views = 0" +
                               "' where streamable like '" + orgUrl + "'";

                        var sqlProcedure = new SqlCommand
                        {
                            Connection = sql,
                            CommandType = CommandType.Text,
                            CommandText = query,
                            CommandTimeout = 10
                        };
                        sqlProcedure.ExecuteNonQuery();

                        File.Delete(@"C:\Users\Kyr\Downloads\Upload\" + prefix + ".mp4");
                    }
                    ClickElementXPath("//div[@data-tooltip='@kyraryc']");
                    ClickElementXPath("//button[contains(text(), 'Log Out')]");
                }
            }
            catch (Exception ex)
            {
                int asdfadf = 0;
            }
        }

        public static void GetFromFolder(string url, string filename, string source)
        {
            string name = url;
            if (Equals(source, "streamable")){
                name = name.Replace("https://streamable.com/","");
            }
            else if (Equals(source, "gfycat"))
            {
                name = name.Replace("https://gfycat.com/","");
            }

            string nameNew = "*" + name + "*";

            string[] fileEntries = Directory.GetFiles(videoFolder, nameNew, SearchOption.AllDirectories);
            if (fileEntries==null)
            {
                fileMissing.Add(url);
            }

            string fileEntry = fileEntries[0];
            string[] filesplit = fileEntry.Split('\\');
            string vidname = filesplit.AsQueryable().Last();
            File.Copy(fileEntry, downloadDirectory + vidname);

            if (uploadImgur)
            {
                UploadImgur(url, vidname, source);
            }
            else
            {
                UploadReddit(url, vidname, source, name);
            }
            
        }

        public static void GetReddit(string URL)
        {
            driver2.Url = URL;

            string filename = driver2.Title;

            filename = filename.Replace(":", "").Replace("respectthreads", "").Replace("Respect", "").Replace("\\", "")
                .Replace("#", "").Replace("%", "").Replace("&", "").Replace("{", "").Replace("}", "").Replace("$", "")
                .Replace("!", "").Replace("'", "").Replace("\"", "").Replace("@", "").Replace("<", "").Replace(">", "")
                .Replace("*", "").Replace("?", "").Replace("/", "").Replace("+", "").Replace("`", "").Replace("|", "")
                .Replace("=", "").Replace(")", "").Replace("(", "").Replace("~", "")
                .Replace("-", "").Replace("^", "").Replace(".", "").Replace(",", "").Trim();

            IList alllinks = driver2.FindElements(By.TagName("a"));

            string rtText = "";

            if (uploadVideos)
            {
                ClickElementXPath("//a[contains(@class, 'edit-usertext')]");
                IWebElement sourceEle = driver2.FindElement(By.XPath("//textarea"));
                rtText = sourceEle.Text;
            }

            ArrayList streams = new ArrayList();
            ArrayList gfys = new ArrayList();
            ArrayList imgurs = new ArrayList();
            ArrayList potentials = new ArrayList();
            for (int i = 0; i < alllinks.Count; i++)
            {
                IWebElement curlink = (IWebElement)alllinks[i];
                string link = curlink.GetAttribute("href");

                if (link == null)
                {
                    continue;
                }
                else if ((link.Contains("streamable.com") || link.Contains("gfycat")) && !potentials.Contains(link))
                {
                    potentials.Add(link);
                }
                else if (link.Contains("imgur.com"))
                {
                    string imgurQuery = "Select * FROM [RedditVids].[dbo].[ImgurLinks] where url like '" + link + "'";

                    var sqlProcedure = new SqlCommand
                    {
                        Connection = sql,
                        CommandType = CommandType.Text,
                        CommandText = imgurQuery,
                        CommandTimeout = 10
                    };

                    var sqlAdapt = new SqlDataAdapter(sqlProcedure);
                    DataTable dt = new DataTable();
                    sqlAdapt.Fill(dt);

                    if (dt.Rows.Count == 0)
                    {
                        //Wasn't found in sql, Insert it into the database for later potential use
                        string insert = "insert into [RedditVids].[dbo].[ImgurLinks] ([Url], [Source], [LastViewed]) values " +
                            "('" + link + "', '" + filename + "', '" + DateTime.Today.AddDays(-100).ToShortDateString() + "')";

                        var sqlProcedureInsert = new SqlCommand
                        {
                            Connection = sql,
                            CommandType = CommandType.Text,
                            CommandText = insert,
                            CommandTimeout = 10
                        };
                        sqlProcedureInsert.ExecuteNonQuery();
                    }
                    if (!imgurs.Contains(link))
                    {
                        imgurs.Add(link);
                    }
                    continue;
                }
                else
                {
                    continue;
                }
            }

            if (!uploadVideos)
            {
                loaded.Add(filename);
                return;
            }
            
            if (uploadImgur)
            {
                newImgur = true;
            }

            for (int c = 0; c < potentials.Count; c++)
            {
                string link = potentials[c].ToString();
                string type = "";

                if (link == null)
                {
                    continue;
                }
                else if (link.Contains("streamable.com"))
                {
                    type = "streamable";
                }
                else if (link.Contains("gfycat"))
                {
                    type = "gfycat";
                }
                else
                {
                    continue;
                }

                //Get link from database
                    string query = "Select * FROM [RedditVids].[dbo].[Links] " +
                    " where " + type + " like '" + link + "'";

                var sqlProcedure = new SqlCommand
                {
                    Connection = sql,
                    CommandType = CommandType.Text,
                    CommandText = query,
                    CommandTimeout = 10
                };

                var sqlAdapt = new SqlDataAdapter(sqlProcedure);
                DataTable dt = new DataTable();
                sqlAdapt.Fill(dt);

                if (dt.Rows.Count == 0)
                {
                    //Wasn't found in sql, Insert it into the database
                    string insert = "insert into [RedditVids].[dbo].[Links] ([" + type + "], [source]) values " +
                        "('" + link + "', '" + filename + "')";

                    var sqlProcedureInsert = new SqlCommand
                    {
                        Connection = sql,
                        CommandType = CommandType.Text,
                        CommandText = insert,
                        CommandTimeout = 10
                    };
                    sqlProcedureInsert.ExecuteNonQuery();
                }
                if (dt.Rows.Count == 0 || string.IsNullOrEmpty(dt.Rows[0]["reddit"].ToString()))
                {
                    if (getFromFolder)
                    {
                        GetFromFolder(link, filename, type);
                    }
                    else if (type == "streamable")
                    {
                        DownloadFromStreamable(link, filename);
                    }
                    else if (type == "gfycat")
                    {
                        DownloadGfy(link, filename);
                    }
                }
                if (type == "streamable" && !streams.Contains(link))
                {
                    streams.Add(link);
                }
                else if (type == "gfycat" && !streams.Contains(link))
                {
                    gfys.Add(link);
                }
            }

            loaded.Add(filename);

            for (int i = 0; i < streams.Count; i++)
            {
                string curLink = streams[i].ToString();
                string query = "Select * from [RedditVids].[dbo].[Links] where streamable like '" + curLink + "'";

                var sqlProcedure = new SqlCommand
                {
                    Connection = sql,
                    CommandType = CommandType.Text,
                    CommandText = query,
                    CommandTimeout = 10
                };

                var sqlAdapt = new SqlDataAdapter(sqlProcedure);
                DataTable dt = new DataTable();
                sqlAdapt.Fill(dt);

                if (dt.Rows.Count == 0)
                {
                    int sdfadf = 0;
                }
                else
                {
                    string newLink = dt.Rows[0]["reddit"].ToString();

                    if (!string.IsNullOrEmpty(newLink))
                    {
                        driver2.Url = newLink;
                        System.Collections.ObjectModel.ReadOnlyCollection<IWebElement> notFound = driver2.FindElements(By.XPath("//p[contains(text(), 'the page you requested does not exist')]"));
                        if (notFound.Count > 0 && !redditFails.Contains(dt.Rows[0]["streamable"].ToString()))
                        {
                            redditFails.Add(dt.Rows[0]["streamable"].ToString());
                            Console.WriteLine("Upload failed " + dt.Rows[0]["streamable"].ToString());
                            string Updatequery = "Delete from [RedditVids].[dbo].[Links] where reddit like '" + newLink + "'";

                            var sqlProcedure2 = new SqlCommand
                            {
                                Connection = sql,
                                CommandType = CommandType.Text,
                                CommandText = Updatequery,
                                CommandTimeout = 10
                            };
                            sqlProcedure2.ExecuteNonQuery();
                            continue;
                        }
                        rtText = rtText.Replace(curLink, newLink);
                    }
                    
                }
            }
            for (int i = 0; i < gfys.Count; i++)
            {
                string curLink = gfys[i].ToString();
                string query = "Select * from [RedditVids].[dbo].[Links] where gfycat like '" + curLink + "'";

                var sqlProcedure = new SqlCommand
                {
                    Connection = sql,
                    CommandType = CommandType.Text,
                    CommandText = query,
                    CommandTimeout = 10
                };

                var sqlAdapt = new SqlDataAdapter(sqlProcedure);
                DataTable dt = new DataTable();
                sqlAdapt.Fill(dt);

                if (dt.Rows.Count == 0)
                {
                    int sdfadf = 0;
                }
                else
                {
                    string newLink = dt.Rows[0]["reddit"].ToString();
                    if (!string.IsNullOrEmpty(newLink))
                    {
                        driver2.Url = newLink;
                        System.Collections.ObjectModel.ReadOnlyCollection<IWebElement> notFound = driver2.FindElements(By.XPath("//p[contains(text(), 'the page you requested does not exist')]"));
                        if (notFound.Count > 0 && !redditFails.Contains(dt.Rows[0]["gfycat"].ToString()))
                        {
                            redditFails.Add(dt.Rows[0]["gfycat"].ToString());
                            Console.WriteLine("Upload failed " + dt.Rows[0]["gfycat"].ToString());
                            string Updatequery = "Delete from [RedditVids].[dbo].[Links] where reddit like '" + newLink + "'";
                            //string Updatequery = "Update [RedditVids].[dbo].[Links] set [reddit] = '' where reddit like '" + newLink + "'";

                            var sqlProcedure2 = new SqlCommand
                            {
                                Connection = sql,
                                CommandType = CommandType.Text,
                                CommandText = Updatequery,
                                CommandTimeout = 10
                            };
                            sqlProcedure2.ExecuteNonQuery();
                            continue;
                        }
                        rtText = rtText.Replace(curLink, newLink);
                    }
                }
            }
            for (int i = 0; i < imgurs.Count; i++)
            {
                string curLink = imgurs[i].ToString();
                string query = "Select * from [RedditVids].[dbo].[ImgurLinks] where Url like '" + curLink + "'";

                var sqlProcedure = new SqlCommand
                {
                    Connection = sql,
                    CommandType = CommandType.Text,
                    CommandText = query,
                    CommandTimeout = 10
                };

                var sqlAdapt = new SqlDataAdapter(sqlProcedure);
                DataTable dt = new DataTable();
                sqlAdapt.Fill(dt);

                if (dt.Rows.Count == 0)
                {
                    int sdfadf = 0;
                }
                else
                {
                    string newLink = dt.Rows[0]["NewUrl"].ToString();
                    if (!string.IsNullOrEmpty(newLink))
                    {
                        rtText = rtText.Replace(curLink, newLink);
                    }
                }
            }

            if (File.Exists(threadsDirectory + filename + ".txt"))
            {
                filename += System.DateTime.Now.Ticks;
            }
            filename += ".txt";
            File.WriteAllText(threadsDirectory + filename, rtText);
        }
        public static void RedditLogin()
        {
            
            driver2.Url = "https://www.reddit.com/login/";
            SetText("loginUsername", redditUsername);
            SetText("loginPassword", redditPassword);
            ClickElementXPath("//button[contains(text(),'Log In')]");
            Thread.Sleep(10000);
        }

        public static void ImgurLogin()
        {
            driver2.Url = "https://imgur.com/signin";
            SetText("username", imgurEmail);
            SetText("password", imgurPassword);
            IWebElement buttonEle = driver2.FindElement(By.ClassName("btn"));
            buttonEle.Click();
            Thread.Sleep(10000);
            driver2.Url = "https://imgur.com/beta/enable";
            Thread.Sleep(5000);
        }
        public static void ClickElementXPath2(string linkText)
        {
            System.Collections.ObjectModel.ReadOnlyCollection<IWebElement> eles = driver2.FindElements(By.XPath(linkText));
            eles[2].Click();
        }
        public static void ClickElementXPath(string linkText)
        {
            IWebElement buttonEle = driver2.FindElement(By.XPath(linkText));
            if (buttonEle == null)
            {
                return;
            }
            buttonEle.Click();
        }
        public static void SetTextName(string id, string text)
        {
            IWebElement labelEle = driver2.FindElement(By.Name(id));
            if (labelEle == null)
            {
                return;
            }
            labelEle.SendKeys(text);
        }
        public static void SetText(string id, string text)
        {
            IWebElement labelEle = driver2.FindElement(By.Id(id));
            if (labelEle == null)
            {
                return;
            }
            labelEle.SendKeys(text);
        }

        public static bool CreateDatabase()
        {
            string testquery = "SELECT TOP 1 * FROM [RedditVids].[dbo].[Links]";
            try
            {
                var sqlProcedure = new SqlCommand
                {
                    Connection = sql,
                    CommandType = CommandType.Text,
                    CommandText = testquery,
                    CommandTimeout = 10
                };

                var sqlAdapt = new SqlDataAdapter(sqlProcedure);
                DataTable dt = new DataTable();
                sqlAdapt.Fill(dt);
            }
            catch (Exception ex)
            {
                //Check if the RedditVids database exists
                string createquery = "Create Database RedditVids";
                try
                {
                    var sqlProcedureDatabase = new SqlCommand
                    {
                        Connection = sql,
                        CommandType = CommandType.Text,
                        CommandText = createquery,
                        CommandTimeout = 10
                    };
                    sqlProcedureDatabase.ExecuteNonQuery();
                }
                catch (Exception ex2)
                {
                    if (!ex2.Message.ToLower().Contains("already exist"))
                    {
                        Console.WriteLine("Unable to create database.  Please open SQL Server and create a RedditVids database");
                        return false;
                    }
                }
                //Attempt to create the Link database
                createquery = "CREATE TABLE [RedditVids].[dbo].[Links](" +
                    " [id][int] IDENTITY(1, 1) NOT NULL, [streamable] [text] NULL, [source] [text] NULL, [gfycat] [text] NULL, [reddit] [text] NULL," +
                    " CONSTRAINT[PK_Links] PRIMARY KEY CLUSTERED ([id] ASC )WITH(PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON[PRIMARY])";

                try
                {
                    var sqlProcedureLink = new SqlCommand
                    {
                        Connection = sql,
                        CommandType = CommandType.Text,
                        CommandText = createquery,
                        CommandTimeout = 10
                    };
                    sqlProcedureLink.ExecuteNonQuery();
                }
                catch (Exception ex2)
                {
                    if (!ex2.Message.ToLower().Contains("already an object named"))
                    {
                        Console.WriteLine("Unable to add the links database.  Please open SQL Server and run the following query");
                        Console.WriteLine(createquery);
                        return false;
                    }
                }
            }

            string imgurQuery = "SELECT TOP 1 * FROM [RedditVids].[dbo].[ImgurLinks]";
            try
            {
                var sqlProcedure = new SqlCommand
                {
                    Connection = sql,
                    CommandType = CommandType.Text,
                    CommandText = imgurQuery,
                    CommandTimeout = 10
                };

                var sqlAdapt = new SqlDataAdapter(sqlProcedure);
                DataTable dt = new DataTable();
                sqlAdapt.Fill(dt);
                return true;
            }
            catch (Exception ex)
            {
                //The database should already exist at this point, just create the table
                //Attempt to create the Link database
                string createquery = "CREATE TABLE [RedditVids].[dbo].[ImgurLinks](" +
                     " [id][int] IDENTITY(1, 1) NOT NULL, [Url] [text] NULL, [Source] [text] NULL, [LastViewed] [date] NULL, [NewUrl] [text] NULL" +
                    " CONSTRAINT[PK_ImgurLinks] PRIMARY KEY CLUSTERED ([id] ASC )WITH(PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON[PRIMARY])";

                try
                {
                    var sqlProcedureLink = new SqlCommand
                    {
                        Connection = sql,
                        CommandType = CommandType.Text,
                        CommandText = createquery,
                        CommandTimeout = 10
                    };
                    sqlProcedureLink.ExecuteNonQuery();
                }
                catch (Exception ex2)
                {
                    if (!ex2.Message.ToLower().Contains("already an object named"))
                    {
                        Console.WriteLine("Unable to add the links database.  Please open SQL Server and run the following query");
                        Console.WriteLine(createquery);
                        return false;
                    }
                }
            }

            return true;
        }

        public static void CheckImgur()
        {
            string query = "SELECT * FROM [RedditVids].[dbo].[ImgurLinks] order by LastViewed";

            var sqlProcedure = new SqlCommand
            {
                Connection = sql,
                CommandType = CommandType.Text,
                CommandText = query,
                CommandTimeout = 10
            };

            var sqlAdapt = new SqlDataAdapter(sqlProcedure);
            DataTable dt = new DataTable();
            sqlAdapt.Fill(dt);

            if (maxImgurChecks > dt.Rows.Count)
            {
                maxImgurChecks = dt.Rows.Count;
            }
            for (int i = 0; i < maxImgurChecks; i++)
            {
                CheckImgurLink(dt.Rows[i]["Url"].ToString(), dt.Rows[i]["Source"].ToString());
            }
        }
        public static void CheckImgurLink(string curUrl, string source)
        {
            Stopwatch sw = new Stopwatch();
            sw.Start();
            driver2.Url = curUrl;

            while (sw.Elapsed.TotalSeconds < 60)
            {
                //experimental NSFW detector
                try
                {
                    IWebElement over18 = driver2.FindElement(By.XPath("//div[contains(text(), 'm over 18')]"));
                    over18.Click();
                }
                catch (Exception ex)
                {
                    //Ok, the image either doesn't exist or is sfw
                }

                try
                {
                    IWebElement notfound = driver2.FindElement(By.XPath("//h1[contains(text(), 'Zoinks! You've taken a wrong turn.')]"));
                    if (!ImgurMissing.Contains(curUrl))
                    {
                        Console.WriteLine("Broken Imgur Link detected - " + curUrl);
                        ImgurMissing.Add(curUrl);
                        ImgurSource.Add(source);
                    }
                    string removeQuery = "Delete from [RedditVids].[dbo].[ImgurLinks]" +
                               " where Url like '" + curUrl + "'";

                    var sqlProcedure2 = new SqlCommand
                    {
                        Connection = sql,
                        CommandType = CommandType.Text,
                        CommandText = removeQuery,
                        CommandTimeout = 10
                    };
                    sqlProcedure2.ExecuteNonQuery();

                    return;
                }
                catch (Exception ex)
                {
                    //an exception here should mean that the Imgur isn't broken
                }
                //One more check just to be sure though
                try
                {
                    IWebElement notfound = driver2.FindElement(By.XPath("//p[contains(text(), 'No webpage was found for the web address:')]"));
                    if (!ImgurMissing.Contains(curUrl))
                    {
                        Console.WriteLine("Broken Imgur Link detected - " + curUrl);
                        ImgurMissing.Add(curUrl);
                        ImgurSource.Add(source);
                    }
                    string removeQuery = "Delete from [RedditVids].[dbo].[ImgurLinks]" +
                               " where Url like '" + curUrl + "'";

                    var sqlProcedure2 = new SqlCommand
                    {
                        Connection = sql,
                        CommandType = CommandType.Text,
                        CommandText = removeQuery,
                        CommandTimeout = 10
                    };
                    sqlProcedure2.ExecuteNonQuery();

                    return;
                }
                catch (Exception ex)
                {
                    //an exception here should mean that the Imgur isn't broken, for real this time
                }

                if (curUrl.Contains("i.imgur.com"))
                {//If its a direct link, go to the main album page. hopefully that avoids any deletions
                    string tempUrl = curUrl.Replace("i.imgur.com", "imgur.com");
                    tempUrl = tempUrl.Substring(0, tempUrl.LastIndexOf("."));
                    driver2.Url = tempUrl;

                    string update = "Update [RedditVids].[dbo].[ImgurLinks] set [LastViewed] = '" + DateTime.Today.ToShortDateString() + "'" +
                            " where Url like '" + curUrl + "'";

                    var sqlProcedureUpdate = new SqlCommand
                    {
                        Connection = sql,
                        CommandType = CommandType.Text,
                        CommandText = update,
                        CommandTimeout = 10
                    };
                    sqlProcedureUpdate.ExecuteNonQuery();
                    return;
                }
                else
                {
                    System.Collections.ObjectModel.ReadOnlyCollection<IWebElement> viewEles = driver2.FindElements(By.XPath("//*[contains(., 'Views')]"));
                    if (viewEles.Count == 0)
                    {//ok, something went wrong. retry all the checks
                        int adsf = 0;
                    }
                    else
                    {
                        break;
                    }
                }
            }
            //Empty gallery check.  if the direct link succeeded, there should be no need to check that

            try
            {
                sw.Restart();
                System.Collections.ObjectModel.ReadOnlyCollection<IWebElement> galleryEles = null;
                while (sw.Elapsed.TotalSeconds < 20)
                {
                    galleryEles = driver2.FindElements(By.XPath("//div[contains(@class, 'Gallery-Content--media')]"));
                    if (galleryEles.Count > 0)
                    {
                        break;
                    }
                }
                if (galleryEles.Count == 0)
                {
                    Console.WriteLine("Possible empty gallery detected: " + curUrl + " - " + source);
                    emptySource.Add(source);
                    emptyGallery.Add(curUrl);
                    return;
                }

            }
            catch (Exception ex)
            {
                //uhh, i guess its fine?
            }
            
            string updateQuery = "Update [RedditVids].[dbo].[ImgurLinks] set [LastViewed] = '" + DateTime.Today.ToShortDateString() + "'" +
                            " where Url like '" + curUrl + "'";

            var sqlProcedureU = new SqlCommand
            {
                Connection = sql,
                CommandType = CommandType.Text,
                CommandText = updateQuery,
                CommandTimeout = 10
            };
            sqlProcedureU.ExecuteNonQuery();
        }
        #endregion
    }
}
