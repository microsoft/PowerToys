// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Wox.Infrastructure.Storage;
using Wox.Plugin.Logger;

namespace Microsoft.Plugin.Program.Storage
{
    internal class Win32ProgramFileSystemWatchers : IDisposable
    {
        public string[] PathsToWatch { get; set; }

        public List<FileSystemWatcherWrapper> FileSystemWatchers { get; set; }

        private bool _disposed;

        // This class contains the list of directories to watch and initializes the File System Watchers
        public Win32ProgramFileSystemWatchers()
        {
            PathsToWatch = GetPathsToWatch();
            SetFileSystemWatchers();
        }

        // Returns an array of paths to be watched
        private static string[] GetPathsToWatch()
        {
            string[] paths = new string[]
                            {
                               Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
                               Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu),
                               Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                               Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory),
                            };

            var invalidPaths = new List<string>();
            foreach (var path in paths)
            {
                try
                {
                    Directory.GetFiles(path);
                }
                catch (Exception ex)
                {
                    Log.Exception($"Failed to get files in {path}", ex, typeof(Win32ProgramFileSystemWatchers));
                    invalidPaths.Add(path);
                }
            }

            return paths.Except(invalidPaths).ToArray();
        }

        // Initializes the FileSystemWatchers
        private void SetFileSystemWatchers()
        {
            FileSystemWatchers = new List<FileSystemWatcherWrapper>();
            for (int index = 0; index < PathsToWatch.Length; index++)
            {
                FileSystemWatchers.Add(new FileSystemWatcherWrapper());
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    for (int index = 0; index < PathsToWatch.Length; index++)
                    {
                        FileSystemWatchers[index].Dispose();
                    }

                    _disposed = true;
                }
            }
        }
    }
}
