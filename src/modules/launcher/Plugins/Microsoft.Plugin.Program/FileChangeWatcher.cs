// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Plugin.Program
{
    //internal static class FileChangeWatcher
    //{
    //    private static readonly List<string> WatchedPath = new List<string>();
    //    // todo remove previous watcher events
    //    public static void AddAll(List<UnregisteredPrograms> sources, string[] suffixes)
    //    {
    //        foreach (var s in sources)
    //        {
    //            if (Directory.Exists(s.Location))
    //            {
    //                AddWatch(s.Location, suffixes);
    //            }
    //        }
    //    }

    //    public static void AddWatch(string path, string[] programSuffixes, bool includingSubDirectory = true)
    //    {
    //        if (WatchedPath.Contains(path)) return;
    //        if (!Directory.Exists(path))
    //        {
    //            Log.Warn($"|FileChangeWatcher|{path} doesn't exist");
    //            return;
    //        }

    //        WatchedPath.Add(path);
    //        foreach (string fileType in programSuffixes)
    //        {
    //            FileSystemWatcher watcher = new FileSystemWatcher
    //            {
    //                Path = path,
    //                IncludeSubdirectories = includingSubDirectory,
    //                Filter = $"*.{fileType}",
    //                EnableRaisingEvents = true
    //            };
    //            watcher.Changed += FileChanged;
    //            watcher.Created += FileChanged;
    //            watcher.Deleted += FileChanged;
    //            watcher.Renamed += FileChanged;
    //        }
    //    }

    //    private static void FileChanged(object source, FileSystemEventArgs e)
    //    {
    //        Task.Run(() =>
    //        {
    //            Main.IndexPrograms();
    //        });
    //    }
    //}
}
