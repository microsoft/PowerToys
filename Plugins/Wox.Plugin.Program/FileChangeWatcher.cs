using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using Wox.Infrastructure;

namespace Wox.Plugin.Program
{
    internal class FileChangeWatcher
    {
        private static bool isIndexing = false;
        private static List<string> watchedPath = new List<string>(); 

        public static void AddWatch(string path, bool includingSubDirectory = true)
        {
            if (watchedPath.Contains(path)) return;
            if (!Directory.Exists(path))
            {
                DebugHelper.WriteLine(string.Format("FileChangeWatcher: {0} doesn't exist", path));
                return;
            }

            watchedPath.Add(path);
            foreach (string fileType in ProgramStorage.Instance.ProgramSuffixes.Split(';'))
            {
                FileSystemWatcher watcher = new FileSystemWatcher
                {
                    Path = path,
                    IncludeSubdirectories = includingSubDirectory,
                    Filter = string.Format("*.{0}", fileType),
                    EnableRaisingEvents = true
                };
                watcher.Changed += FileChanged;
                watcher.Created += FileChanged;
                watcher.Deleted += FileChanged;
                watcher.Renamed += FileChanged;
            }
        }

        private static void FileChanged(object source, FileSystemEventArgs e)
        {
            if (!isIndexing)
            {
                ThreadPool.QueueUserWorkItem(o =>
                {
                    Programs.IndexPrograms();
                    isIndexing = false;
                });
            }
        }
 
    }
}
