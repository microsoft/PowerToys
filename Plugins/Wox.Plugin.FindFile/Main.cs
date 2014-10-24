using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using Wox.Infrastructure;
using Wox.Infrastructure.MFTSearch;

namespace Wox.Plugin.FindFile
{
    public class Main : IPlugin
    {
        private PluginInitContext context;
        private bool initial = false;

        public List<Result> Query(Query query)
        {
            if (!initial)
            {
                return new List<Result>()
                {
                    new Result("Wox is indexing your files, please try later.","Images/warning.png")
                };
            }

            string q = query.GetAllRemainingParameter();
            return MFTSearcher.Search(q).Take(100).Select(t => ConvertMFTSearch(t, q)).ToList();
        }

        public void Init(PluginInitContext context)
        {
            this.context = context;
            var searchtimestart = DateTime.Now;
            MFTSearcher.IndexAllVolumes();
            initial = true;
            var searchtimeend = DateTime.Now;
            Debug.WriteLine(string.Format("{0} file, indexed, {1}ms has spent.", MFTSearcher.IndexedFileCount, searchtimeend.Subtract(searchtimestart).TotalMilliseconds));
        }

        private Result ConvertMFTSearch(MFTSearchRecord record, string query)
        {
            string icoPath = "Images/file.png";
            if (record.IsFolder)
            {
                icoPath = "Images/folder.png";
            }

            string name = Path.GetFileName(record.FullPath);
            FuzzyMatcher matcher = FuzzyMatcher.Create(query);
            return new Result()
            {
                Title = name,
                Score = matcher.Evaluate(name).Score,
                SubTitle = record.FullPath,
                IcoPath = icoPath,
                Action = _ =>
                {
                    try
                    {
                        Process.Start(record.FullPath);
                    }
                    catch
                    {
                        context.API.ShowMsg("Can't open " + record.FullPath, string.Empty, string.Empty);
                        return false;
                    }
                    return true;
                },
                ContextMenu = GetContextMenu(record)
            };
        }

        private List<Result> GetContextMenu(MFTSearchRecord record)
        {
            List<Result> contextMenus = new List<Result>();

            if (!record.IsFolder)
            {
                contextMenus.Add(new Result()
                {
                    Title = "Open Containing Folder",
                    Action = _ =>
                    {
                        try
                        {
                            Process.Start("explorer.exe", string.Format("/select, \"{0}\"", record.FullPath));
                        }
                        catch
                        {
                            context.API.ShowMsg("Can't open " + record.FullPath, string.Empty, string.Empty);
                            return false;
                        }
                        return true;
                    },
                    IcoPath = "Images/folder.png"
                });
            }

            return contextMenus;
        }
    }
}
