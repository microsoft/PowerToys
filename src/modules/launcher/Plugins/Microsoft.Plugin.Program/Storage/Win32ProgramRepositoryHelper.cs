using System;
using System.Collections.Generic;
using Wox.Infrastructure.Storage;

namespace Microsoft.Plugin.Program.Storage
{
    internal class Win32ProgramRepositoryHelper
    {

        public readonly string[] _pathsToWatch;
        public List<FileSystemWatcherWrapper> _fileSystemWatchers;

        // This class contains the list of directories to watch and initializes the File System Watchers
        public Win32ProgramRepositoryHelper()
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

    }
}
