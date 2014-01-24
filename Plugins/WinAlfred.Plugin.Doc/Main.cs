using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace WinAlfred.Plugin.Doc
{
    public class Main : IPlugin
    {
        static public string AssemblyDirectory
        {
            get
            {
                string codeBase = Assembly.GetExecutingAssembly().CodeBase;
                UriBuilder uri = new UriBuilder(codeBase);
                string path = Uri.UnescapeDataString(uri.Path);
                return Path.GetDirectoryName(path);
            }
        }

        public List<Result> Query(Query query)
        {
            string path = @"D:\Personal\WinAlfred\WinAlfred\bin\Debug\Plugins\Doc\Docset\jQuery.docset\Contents\Resources\docSet.dsidx";
            if (query.ActionParameters.Count == 0)
            {
                //todo:return available docsets name
                return new List<Result>();
            }
            return QuerySqllite(path, query.ActionParameters[0]);
        }

        public void Init(PluginInitContext context)
        {
            //todo:move to common place
            var otherCompanyDlls = new DirectoryInfo(AssemblyDirectory + "\\Plugins\\Doc").GetFiles("*.dll");
            AppDomain.CurrentDomain.AssemblyResolve += (sender, args) =>
                {
                    var dll = otherCompanyDlls.FirstOrDefault(fi =>
                        {
                            try
                            {
                                Assembly assembly = Assembly.LoadFile(fi.FullName);
                                return assembly.FullName == args.Name;
                            }
                            catch
                            {
                                return false;
                            }
                        });
                    if (dll == null)
                    {
                        return null;
                    }

                    return Assembly.LoadFile(dll.FullName);
                };
        }

        public List<Result> QuerySqllite(string path, string key)
        {
            SQLiteConnection conn = null;
            string dbPath = "Data Source =" + path;
            conn = new SQLiteConnection(dbPath);
            conn.Open();
            string sql = "select * from searchIndex where name like '%" + key + "%'";
            SQLiteCommand cmdQ = new SQLiteCommand(sql, conn);
            SQLiteDataReader reader = cmdQ.ExecuteReader();

            List<Result> results = new List<Result>();
            while (reader.Read())
            {
                string name = reader.GetString(1);
                string type = reader.GetString(2);
                string docPath = reader.GetString(3);

                results.Add(new Result
                    {
                        Title = name,
                        SubTitle = AssemblyDirectory + "\\Plugins\\Doc\\Docset\\" +  docPath,
                        Action = () =>
                            {
                                DocViewFrm frm = new DocViewFrm();
                                frm.ShowDoc(AssemblyDirectory + @"\Plugins\Doc\Docset\jQuery.docset\Contents\Resources\Documents\" + docPath);
                            }
                    });
            }
            conn.Close();

            return results;
        }
    }
}
