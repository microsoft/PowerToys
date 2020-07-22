using System;
using System.Collections.Generic;
using Wox.Infrastructure.Storage;

namespace Microsoft.Plugin.Program.Storage
{
    internal class Win32ProgramFileSystemWatchers : IDisposable
    {

        public readonly string[] _pathsToWatch;
        public List<FileSystemWatcherWrapper> _fileSystemWatchers;
        private bool _disposed = false;

        // This class contains the list of directories to watch and initializes the File System Watchers
        public Win32ProgramFileSystemWatchers()
        {
            _pathsToWatch = GetPathsToWatch();
            SetFileSystemWatchers();
        }

        // Returns an array of paths to be watched
        private string[] GetPathsToWatch()
        {
            string[] paths = new string[]
                            {
                               Environment.GetFolderPath(Environment.SpecialFolder.Programs),
                               Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms),
                               Environment.GetFolderPath(Environment.SpecialFolder.Desktop)
                            };
            return paths;
        }

        // Initializes the FileSystemWatchers
        private void SetFileSystemWatchers()
        {
            _fileSystemWatchers = new List<FileSystemWatcherWrapper>();
            for (int index = 0; index < _pathsToWatch.Length; index++)
            {
                _fileSystemWatchers.Add(new FileSystemWatcherWrapper());
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
                    for(int index = 0; index < _pathsToWatch.Length; index++)
                    {
                        _fileSystemWatchers[index].Dispose();
                    }
                    _disposed = true;
                }
            }
        }

    }
}
