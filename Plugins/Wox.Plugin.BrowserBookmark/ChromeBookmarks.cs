using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Wox.Plugin.BrowserBookmark
{
    public class ChromeBookmarks
    {
        private List<Bookmark> bookmarks = new List<Bookmark>();

        public List<Bookmark> GetBookmarks()
        {
            bookmarks.Clear();
            LoadChromeBookmarks();

            return bookmarks;
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
}