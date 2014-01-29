using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Newtonsoft.Json;
using Wox.Plugin.System.Common;

namespace Wox.Plugin.System
{
    public class BrowserBookmarks : BaseSystemPlugin
    {

        private List<Bookmark> bookmarks = new List<Bookmark>();

        [DllImport("shell32.dll")]
        static extern bool SHGetSpecialFolderPath(IntPtr hwndOwner, [Out] StringBuilder lpszPath, int nFolder, bool fCreate);
        const int CSIDL_LOCAL_APPDATA = 0x001c;

        protected override List<Result> QueryInternal(Query query)
        {
            if (string.IsNullOrEmpty(query.RawQuery) || query.RawQuery.EndsWith(" ") || query.RawQuery.Length <= 1) return new List<Result>();

            List<Bookmark> returnList = bookmarks.Where(o => MatchProgram(o, query)).ToList();

            return returnList.Select(c => new Result()
            {
                Title = c.Name,
                SubTitle = "Bookmark: " + c.Url,
                IcoPath = Directory.GetCurrentDirectory() + @"\Images\bookmark.png",
                Score = 5,
                Action = () =>
                {
                    try
                    {
                        Process.Start(c.Url);
                    }
                    catch (Exception e)
                    {
                        MessageBox.Show("open url failed:" + c.Url);
                    }
                }
            }).ToList();
        }

        private bool MatchProgram(Bookmark bookmark, Query query)
        {
            if (bookmark.Name.ToLower().Contains(query.RawQuery.ToLower()) || bookmark.Url.ToLower().Contains(query.RawQuery.ToLower())) return true;
            if (ChineseToPinYin.ToPinYin(bookmark.Name).Replace(" ", "").ToLower().Contains(query.RawQuery.ToLower())) return true;

            return false;
        }

        protected override void InitInternal(PluginInitContext context)
        {
            LoadChromeBookmarks();
        }

        private void LoadChromeBookmarks()
        {
            StringBuilder platformPath = new StringBuilder(560);
            SHGetSpecialFolderPath(IntPtr.Zero, platformPath, CSIDL_LOCAL_APPDATA, false);

            string path = platformPath + @"\Google\Chrome\User Data\Default\Bookmarks";
            if (File.Exists(path))
            {
                string all = File.ReadAllText(path);
                Regex nameRegex = new Regex("\"name\": \"(?<name>.*?)\"");
                MatchCollection nameCollection = nameRegex.Matches(all);
                Regex typeRegex = new Regex("\"type\": \"(?<type>.*?)\"");
                MatchCollection typeCollection = typeRegex.Matches(all);
                Regex urlRegex = new Regex("\"url\": \"(?<url>.*?)\"");
                MatchCollection urlCollection = urlRegex.Matches(all);

                List<string> names = (from Match match in nameCollection select match.Groups["name"].Value).ToList();
                List<string> types = (from Match match in typeCollection select match.Groups["type"].Value).ToList();
                List<string> urls = (from Match match in urlCollection select match.Groups["url"].Value).ToList();

                int urlIndex = 0;
                for (int i = 0; i < names.Count; i++)
                {
                    string name = DecodeUnicode(names[i]);
                    string type = types[i];
                    if (type == "url")
                    {
                        string url = urls[urlIndex];
                        urlIndex++;

                        bookmarks.Add(new Bookmark()
                        {
                            Name = name,
                            Url = url,
                            Source = "Chrome"
                        });
                    }
                }
            }
            else
            {
#if (DEBUG)
                {
                    MessageBox.Show("load chrome bookmark failed");
                }
#endif
            }
        }

        private String DecodeUnicode(String dataStr)
        {
            Regex reg = new Regex(@"(?i)\\[uU]([0-9a-f]{4})");
            return reg.Replace(dataStr, m => ((char)Convert.ToInt32(m.Groups[1].Value, 16)).ToString());
        }
    }

    public class Bookmark
    {
        public string Name { get; set; }
        public string Url { get; set; }
        public string Source { get; set; }
    }
}
