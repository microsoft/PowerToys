using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Wox.Infrastructure.Logger;

namespace Wox.Plugin.Program
{
    internal class FileChangeWatcher
    {
        private static bool isIndexing;
        private static List<string> watchedPath = new List<string>(); 

        public static void AddWatch(string path, string[] programSuffixes, bool includingSubDirectory = true)
        {
            if (watchedPath.Contains(path)) return;
            if (!Directory.Exists(path))
            {
                Log.Warn($"FileChangeWatcher: {path} doesn't exist");
                return;
            }

            watchedPath.Add(path);
            foreach (string fileType in programSuffixes)
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
                Task.Run(() =>
                {
                    Programs.IndexPrograms();
                    isIndexing = false;
                });
            }
        }
 
    }
}
