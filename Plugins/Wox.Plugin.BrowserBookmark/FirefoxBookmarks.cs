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

            // create the connection string and init the connection
            string dbPath = string.Format(dbPathFormat, PlacesPath);
            var dbConnection = new SQLiteConnection(dbPath);

            // Open connection to the database file and execute the query
            dbConnection.Open();
            var reader = new SQLiteCommand(queryAllBookmarks, dbConnection).ExecuteReader();

            // return results in List<Bookmark> format
            return reader.Select(x => new Bookmark()
            {
                Name = (x["title"] is DBNull) ? string.Empty : x["title"].ToString(),
                Url = x["url"].ToString()
            }).ToList();
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
                var lines = ini.Split(new string[] { "\r\n" }, StringSplitOptions.None).ToList();

                var index = lines.IndexOf("Default=1");
                if (index > 3) {
                    var relative = lines[index - 2].Split('=')[1];
                    var profiePath = lines[index - 1].Split('=')[1];
                    return relative == "0"
                        ? profiePath + @"\places.sqlite"
                        : Path.Combine(profileFolderPath, profiePath) + @"\places.sqlite";
                }
                return string.Empty;
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
