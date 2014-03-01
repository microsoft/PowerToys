using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Web;
using Microsoft.Win32;

namespace Wox.Plugin.Doc
{
    public class Main : IPlugin
    {
        private List<Doc> docs = new List<Doc>();
        DocViewFrm frm = new DocViewFrm();
        private string docsetBasePath;

        public List<Result> Query(Query query)
        {
            List<Result> results = new List<Result>();
            if (query.ActionParameters.Count == 0)
            {
                results.AddRange(docs.Select(o => new Result()
                {
                    Title = o.Name.Replace(".docset", "").Replace(" ",""),
                    IcoPath = o.IconPath
                }).ToList());
                return results;
            }

            if (query.ActionParameters.Count >= 2)
            {
                Doc firstOrDefault = docs.FirstOrDefault(o => o.Name.Replace(".docset", "").Replace(" ","").ToLower() == query.ActionParameters[0]);
                if (firstOrDefault != null)
                {
                    results.AddRange(QuerySqllite(firstOrDefault, query.ActionParameters[1]));
                }
            }
            else
            {
                foreach (Doc doc in docs)
                {
                    results.AddRange(QuerySqllite(doc, query.ActionParameters[0]));
                }
            }
            

            return results;
        }

        public void Init(PluginInitContext context)
        {
            docsetBasePath = context.CurrentPluginMetadata.PluginDirecotry + @"Docset";
            if (!Directory.Exists(docsetBasePath))
                Directory.CreateDirectory(docsetBasePath);

            foreach (string path in Directory.GetDirectories(docsetBasePath))
            {
                string name = path.Substring(path.LastIndexOf('\\') + 1);
                string dbPath = path + @"\Contents\Resources\docSet.dsidx";
                string dbType = CheckTableExists("searchIndex", dbPath) ? "DASH" : "ZDASH";
                docs.Add(new Doc
                {
                    Name = name,
                    DBPath = dbPath,
                    DBType = dbType,
                    IconPath = TryGetIcon(name, path)
                });
            }
        }

        private string TryGetIcon(string name, string path)
        {
            string url = "https://raw.github.com/jkozera/zeal/master/zeal/icons/" +
                         name.Replace(".docset", "").Replace(" ", "_") + ".png";
            string imagePath = path + "\\icon.png";
            if (!File.Exists(imagePath))
            {
                HttpWebRequest lxRequest = (HttpWebRequest)WebRequest.Create(url);
                // returned values are returned as a stream, then read into a string
                String lsResponse = string.Empty;
                using (HttpWebResponse lxResponse = (HttpWebResponse)lxRequest.GetResponse())
                {
                    using (BinaryReader reader = new BinaryReader(lxResponse.GetResponseStream()))
                    {
                        Byte[] lnByte = reader.ReadBytes(1 * 1024 * 1024 * 10);
                        using (FileStream lxFS = new FileStream(imagePath, FileMode.Create))
                        {
                            lxFS.Write(lnByte, 0, lnByte.Length);
                        }
                    }
                }
            }
            return imagePath;
        }

        private List<Result> QuerySqllite(Doc doc, string key)
        {
            string dbPath = "Data Source =" + doc.DBPath;
            SQLiteConnection conn = new SQLiteConnection(dbPath);
            conn.Open();
            string sql = GetSqlByDocDBType(doc.DBType).Replace("{0}", key);
            SQLiteCommand cmdQ = new SQLiteCommand(sql, conn);
            SQLiteDataReader reader = cmdQ.ExecuteReader();

            List<Result> results = new List<Result>();
            while (reader.Read())
            {
                string name = reader.GetString(reader.GetOrdinal("name"));
                string docPath = reader.GetString(reader.GetOrdinal("path"));

                results.Add(new Result
                    {
                        Title = name,
                        SubTitle = doc.Name.Replace(".docset", ""),
                        IcoPath = doc.IconPath,
                        Action = (c) =>
                        {
                            string url = string.Format(@"{0}\{1}\Contents\Resources\Documents\{2}#{3}", docsetBasePath,
                                doc.Name, docPath, name);

                            //frm.ShowDoc(url);
                            string browser = GetDefaultBrowserPath();
                            Process.Start(browser, String.Format("\"file:///{0}\"", url));
                            return true;
                        }
                    });
            }

            conn.Close();

            return results;
        }

        private static string GetDefaultBrowserPath()
        {
            string key = @"HTTP\shell\open\command";
            using (RegistryKey registrykey = Registry.ClassesRoot.OpenSubKey(key, false))
            {
                if (registrykey != null) return ((string)registrykey.GetValue(null, null)).Split('"')[1];
            }
            return null;
        }

        private string GetSqlByDocDBType(string type)
        {
            string sql = string.Empty;
            if (type == "DASH")
            {
                sql = "select * from searchIndex where name like '%{0}%' order by name asc, path asc limit 30";
            }
            if (type == "ZDASH")
            {
                sql = @"select ztokenname as name, zpath as path from ztoken 
join ztokenmetainformation on ztoken.zmetainformation = ztokenmetainformation.z_pk
join zfilepath on ztokenmetainformation.zfile = zfilepath.z_pk
where (ztokenname like '%{0}%') order by lower(ztokenname) asc, zpath asc limit 30";
            }

            return sql;
        }

        private bool CheckTableExists(string table, string path)
        {
            string dbPath = "Data Source =" + path;
            SQLiteConnection conn = new SQLiteConnection(dbPath);
            conn.Open();
            string sql = "SELECT name FROM sqlite_master WHERE type='table' AND name='" + table + "';";
            SQLiteCommand cmdQ = new SQLiteCommand(sql, conn);
            object obj = cmdQ.ExecuteScalar();
            conn.Close();
            return obj != null;
        }
    }
}
