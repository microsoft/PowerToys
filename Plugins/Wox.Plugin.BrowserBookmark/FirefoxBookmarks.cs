using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Linq;

namespace Wox.Plugin.BrowserBookmark
{
    public class FirefoxBookmarks
    {
        private const string queryBookmarks = @"SELECT url, title
              FROM moz_places
              WHERE id in (
                SELECT bm.fk FROM moz_bookmarks bm WHERE bm.fk NOT NULL
              )
              AND ( url LIKE '%{0}%' OR title LIKE '%{0}%' )
              ORDER BY visit_count DESC
              LIMIT 20
            ";

        private const string queryTopBookmarks = @"SELECT url, title
              FROM moz_places
              WHERE id in (
                SELECT bm.fk FROM moz_bookmarks bm WHERE bm.fk NOT NULL
              )
              ORDER BY visit_count DESC
              LIMIT 20
            ";

        private const string dbPathFormat = "Data Source ={0};Version=3;New=False;Compress=True;";

        public List<Bookmark> GetBookmarks(string search = null, bool top = false)
        {
            // Create the query command for the given case
            string query = top ? queryTopBookmarks : string.Format(queryBookmarks, search);

            return GetResults(query);
        }

        /// <summary>
        /// Searches the places.sqlite db based on the given query and returns the results
        /// </summary>
        private List<Bookmark> GetResults(string query)
        {
            // create the connection string and init the connection
            string dbPath = string.Format(dbPathFormat, PlacesPath);
            var dbConnection = new SQLiteConnection(dbPath);

            // Open connection to the database file and execute the query
            dbConnection.Open();
            var reader = new SQLiteCommand(query, dbConnection).ExecuteReader();

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
                var profilesPath = Environment.ExpandEnvironmentVariables(@"%appdata%\Mozilla\Firefox\Profiles\");
                var folders = new DirectoryInfo(profilesPath).GetDirectories().Select(x => x.FullName).ToList();

                // Look for the default profile folder
                return string.Format(@"{0}\places.sqlite",
                                     folders.FirstOrDefault(d => File.Exists(d + @"\places.sqlite") && d.EndsWith(".default"))
                                     ?? folders.First(d => File.Exists(d + @"\places.sqlite")));
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
