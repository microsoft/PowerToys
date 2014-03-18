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
using Wox.Infrastructure;

namespace Wox.Plugin.System
{
    public class BrowserBookmarks : BaseSystemPlugin
    {

        private List<Bookmark> bookmarks = new List<Bookmark>();

        protected override List<Result> QueryInternal(Query query)
        {
            if (string.IsNullOrEmpty(query.RawQuery) || query.RawQuery.EndsWith(" ") || query.RawQuery.Length <= 1) return new List<Result>();

            var fuzzyMather = FuzzyMatcher.Create(query.RawQuery);
            List<Bookmark> returnList = bookmarks.Where(o => MatchProgram(o, query.RawQuery.ToLower())).ToList();
            return returnList.Select(c => new Result()
            {
                Title = c.Name,
                SubTitle = "Bookmark: " + c.Url,
                IcoPath = Directory.GetCurrentDirectory() + @"\Images\bookmark.png",
                Score = 5,
                Action = (context) =>
                {
                    try
                    {
                        Process.Start(c.Url);
                    }
                    catch (Exception)
                    {
                        MessageBox.Show("open url failed:" + c.Url);
                    }
                    return true;
                }
            }).ToList();
        }
        private bool MatchProgram(Bookmark bookmark, String query)
        {
            if (bookmark.Name.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (bookmark.Url.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (bookmark.PinyinName.Contains(query)) return true;
            return false;
        }

        protected override void InitInternal(PluginInitContext context)
        {
            bookmarks.Clear();
            LoadChromeBookmarks();
         
            bookmarks = bookmarks.Distinct().ToList();
        }

        private void ParseChromeBookmarks(String path, string source)
        {
            if (!File.Exists(path)) return;

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

                    if (url == null) continue;
                    if (url.StartsWith("javascript:", StringComparison.OrdinalIgnoreCase)) continue;
                    if (url.StartsWith("vbscript:", StringComparison.OrdinalIgnoreCase)) continue;

                    bookmarks.Add(new Bookmark()
                    {
                        Name = name,
                        Url = url,
                        Source = source
                    });
                }
            }
        }

        private void LoadChromeBookmarks(string path, string name)
        {
            if (!Directory.Exists(path)) return;
            var paths = Directory.GetDirectories(path);

            foreach (var profile in paths)
            {
                if (File.Exists(Path.Combine(profile, "Bookmarks")))
                    ParseChromeBookmarks(Path.Combine(profile, "Bookmarks"), name + (Path.GetFileName(profile) == "Default" ? "" : (" (" + Path.GetFileName(profile) + ")")));
            }
        }

        private void LoadChromeBookmarks()
        {
            String platformPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            LoadChromeBookmarks(Path.Combine(platformPath, @"Google\Chrome\User Data"), "Google Chrome");
            LoadChromeBookmarks(Path.Combine(platformPath, @"Google\Chrome SxS\User Data"), "Google Chrome Canary");
            LoadChromeBookmarks(Path.Combine(platformPath, @"Chromium\User Data"), "Chromium");
        }

        private String DecodeUnicode(String dataStr)
        {
            Regex reg = new Regex(@"(?i)\\[uU]([0-9a-f]{4})");
            return reg.Replace(dataStr, m => ((char)Convert.ToInt32(m.Groups[1].Value, 16)).ToString());
        }
    }

    public class Bookmark : IEquatable<Bookmark>, IEqualityComparer<Bookmark>
    {
        private string m_Name;
        public string Name { 
            get{
                return m_Name;
            }
            set
            {
                m_Name = value;
                PinyinName = ChineseToPinYin.ToPinYin(m_Name).Replace(" ", "").ToLower();
            }
        }
        public string PinyinName { get; private set; }
        public string Url { get; set; }
        public string Source { get; set; }

        /* TODO: since Source maybe unimportant, we just need to compare Name and Url */
        public bool Equals(Bookmark other)
        {
            return Equals(this, other);
        }

        public bool Equals(Bookmark x, Bookmark y)
        {
            if (Object.ReferenceEquals(x, y)) return true;
            if (Object.ReferenceEquals(x, null) || Object.ReferenceEquals(y, null))
                return false;

            return x.Name == y.Name && x.Url == y.Url;
        }

        public int GetHashCode(Bookmark bookmark)
        {
            if (Object.ReferenceEquals(bookmark, null)) return 0;
            int hashName = bookmark.Name == null ? 0 : bookmark.Name.GetHashCode();
            int hashUrl = bookmark.Url == null ? 0 : bookmark.Url.GetHashCode();
            return hashName ^ hashUrl;
        }

        public override int GetHashCode()
        {
            return GetHashCode(this);
        }
    }
}
