using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Linq;

namespace Wox.Plugin.BrowserBookmark
{
    public class FirefoxBookmarks
    {
        private const string queryAllBookmarks = @"SELECT moz_places.url, moz_bookmarks.title
              FROM moz_places
              INNER JOIN moz_bookmarks ON (
                moz_bookmarks.fk NOT NULL AND moz_bookmarks.fk = moz_places.id
              )              
              ORDER BY moz_places.visit_count DESC
            ";

        private const string dbPathFormat = "Data Source ={0};Version=3;New=False;Compress=True;";

        /// <summary>
        /// Searches the places.sqlite db and returns all bookmarks
        /// </summary>
        public List<Bookmark> GetBookmarks()
        {
            // Return empty list if the places.sqlite file cannot be found
            if (string.IsNullOrEmpty(PlacesPath) || !File.Exists(PlacesPath))
                return new List<Bookmark>();

            var bookmarList = new List<Bookmark>();

            // create the connection string and init the connection
            string dbPath = string.Format(dbPathFormat, PlacesPath);            
            using (var dbConnection = new SQLiteConnection(dbPath))
            {
                // Open connection to the database file and execute the query
                dbConnection.Open();
                var reader = new SQLiteCommand(queryAllBookmarks, dbConnection).ExecuteReader();

                // return results in List<Bookmark> format
                bookmarList = reader.Select(x => new Bookmark()
                {
                    Name = (x["title"] is DBNull) ? string.Empty : x["title"].ToString(),
                    Url = x["url"].ToString()
                }).ToList();
            }

            return bookmarList;
        }

        /// <summary>
        /// Path to places.sqlite
        /// </summary>
        private string PlacesPath
        {
            get
            {
                var profileFolderPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"Mozilla\Firefox");
                var profileIni = Path.Combine(profileFolderPath, @"profiles.ini");

                if (!File.Exists(profileIni))
                    return string.Empty;

                // get firefox default profile directory from profiles.ini
                string ini;
                using (var sReader = new StreamReader(profileIni)) {
                    ini = sReader.ReadToEnd();
                }

                /*
                    Current profiles.ini structure example as of Firefox version 69.0.1
                    
                    [Install736426B0AF4A39CB]
                    Default=Profiles/7789f565.default-release   <== this is the default profile this plugin will get the bookmarks from. When opened Firefox will load the default profile
                    Locked=1

                    [Profile2]
                    Name=newblahprofile
                    IsRelative=0
                    Path=C:\t6h2yuq8.newblahprofile  <== Note this is a custom location path for the profile user can set, we need to cater for this in code.

                    [Profile1]
                    Name=default
                    IsRelative=1
                    Path=Profiles/cydum7q4.default
                    Default=1

                    [Profile0]
                    Name=default-release
                    IsRelative=1
                    Path=Profiles/7789f565.default-release

                    [General]
                    StartWithLastProfile=1
                    Version=2
                */

                var lines = ini.Split(new string[] { "\r\n" }, StringSplitOptions.None).ToList();

                var defaultProfileFolderNameRaw = lines.Where(x => x.Contains("Default=") && x != "Default=1").FirstOrDefault() ?? string.Empty;

                if (string.IsNullOrEmpty(defaultProfileFolderNameRaw))
                    return string.Empty;

                var defaultProfileFolderName = defaultProfileFolderNameRaw.Split('=').Last();

                var indexOfDefaultProfileAtttributePath = lines.IndexOf("Path="+ defaultProfileFolderName);

                // Seen in the example above, the IsRelative attribute is always above the Path attribute
                var relativeAttribute = lines[indexOfDefaultProfileAtttributePath - 1];

                return relativeAttribute == "0" // See above, the profile is located in a custom location, path is not relative, so IsRelative=0
                        ? defaultProfileFolderName + @"\places.sqlite"
                        : Path.Combine(profileFolderPath, defaultProfileFolderName) + @"\places.sqlite";
            }
        }
    }

    public static class Extensions
    {
        public static IEnumerable<T> Select<T>(this SQLiteDataReader reader, Func<SQLiteDataReader, T> projection)
        {
            while (reader.Read())
            {
                yield return projection(reader);
            }
        }
    }
}
