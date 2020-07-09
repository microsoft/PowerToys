using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Microsoft.Plugin.Program.Programs;
using Wox.Infrastructure.Storage;
using System.IO;
using System.Linq;

namespace Microsoft.Plugin.Program.Storage
{
    internal class Win32ProgramRepository : ListRepository<Programs.Win32>, IProgramRepository
    {
        private IStorage<IList<Programs.Win32>> _storage;
        private Settings _settings;
        private List<IFileSystemWatcherWrapper> _fileSystemWatcherHelpers;
        private string[] _pathsToWatch;
        private int _numberOfPathsToWatch;


        public Win32ProgramRepository(List<IFileSystemWatcherWrapper> fileSystemWatcherHelpers, IStorage<IList<Programs.Win32>> storage, Settings settings, string[] pathsToWatch)
        {
            this._fileSystemWatcherHelpers = new List<IFileSystemWatcherWrapper>(fileSystemWatcherHelpers);
            this._storage = storage ?? throw new ArgumentNullException("storage", "Win32ProgramRepository requires an initialized storage interface");
            this._settings = settings ?? throw new ArgumentNullException("settings", "Win32ProgramRepository requires an initialized settings object");
            this._pathsToWatch = pathsToWatch;
            this._numberOfPathsToWatch = pathsToWatch.Count();
            InitializeFileSystemWatchers();
        }

        private void InitializeFileSystemWatchers()
        {
            for(int index = 0; index < _numberOfPathsToWatch; index++)
            {
                // To set the paths to monitor
                _fileSystemWatcherHelpers[index].Path = _pathsToWatch[index];

                // to be notified when there is a change to a file/directory
                _fileSystemWatcherHelpers[index].NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName;

                // filtering the app types that we want to monitor
                _fileSystemWatcherHelpers[index].Filters = new Collection<String>{ "*.exe", "*.appref-ms", "*.lnk", "*.url"};

                // Registering the event handlers
                _fileSystemWatcherHelpers[index].Created += OnAppCreated;
                _fileSystemWatcherHelpers[index].Deleted += OnAppDeleted;
                _fileSystemWatcherHelpers[index].Renamed += OnAppRenamed;

                // Enable the file system watcher
                _fileSystemWatcherHelpers[index].EnableRaisingEvents = true;
            }
        }

        private void OnAppRenamed(object sender, RenamedEventArgs e)
        {
            throw new NotImplementedException();
        }

        private void OnAppDeleted(object sender, FileSystemEventArgs e)
        {
            throw new NotImplementedException();
        }

        private void OnAppCreated(object sender, FileSystemEventArgs e)
        {
            throw new NotImplementedException();
        }

        public void IndexPrograms()
        {
            var applications = Programs.Win32.All(_settings);
            Set(applications);
        }

        public void Save()
        {
            _storage.Save(Items);
        }

        public void Load()
        {
            var items = _storage.TryLoad(new Programs.Win32[] { });
            Set(items);
        }

    }
}
