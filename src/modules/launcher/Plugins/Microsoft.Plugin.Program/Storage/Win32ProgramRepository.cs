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
        private Collection<string> extensionsToWatch = new Collection<string>{ "*.exe", "*.lnk", "*.appref-ms", "*.url" };
        private readonly string lnkExtension = ".lnk";

        public Win32ProgramRepository(List<IFileSystemWatcherWrapper> fileSystemWatcherHelpers, IStorage<IList<Programs.Win32>> storage, Settings settings, string[] pathsToWatch)
        {
            this._fileSystemWatcherHelpers = fileSystemWatcherHelpers;
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

                // to be notified when there is a change to a file
                _fileSystemWatcherHelpers[index].NotifyFilter = NotifyFilters.FileName;

                // filtering the app types that we want to monitor
                _fileSystemWatcherHelpers[index].Filters = extensionsToWatch;

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
            string oldPath = e.OldFullPath;
            string newPath = e.FullPath;

            string extension = Path.GetExtension(newPath);
            Programs.Win32 newApp = Programs.Win32.GetAppFromPath(newPath);
            Programs.Win32 oldApp;

            // Once the shortcut application is renamed, the old app does not exist and therefore when we try to get the FullPath we get the lnk path instead of the exe path
            // This changes the hashCode() of the old application.
            // Therefore, instead of retrieving the old app using the GetAppFromPath(), we construct the application ourself
            // This situation is not encountered for other application types because the fullPath is the path itself, instead of being computed by using the path to the app.
            if (extension.Equals(lnkExtension, StringComparison.OrdinalIgnoreCase))
            {
                oldApp = new Programs.Win32() { Name = GetNameOfLnkApp(e.OldName), ExecutableName = newApp.ExecutableName, FullPath = newApp.FullPath };
            }
            else
            {
                oldApp = Programs.Win32.GetAppFromPath(oldPath);
            }

            // To remove the old app which has been renamed and to add the new application.
            if (oldApp != null)
            {
                Remove(oldApp);
            }

            if (newApp != null)
            {
                Add(newApp);
            }
        }

        // Remove Extension to return the Name of the app
        // Eg: Notepad.lnk must return NotePad.
        private string GetNameOfLnkApp(string oldName)
        {
            int length = oldName.Length;
            return oldName.Substring(0, oldName.Length - lnkExtension.Length);
        }

        private void OnAppDeleted(object sender, FileSystemEventArgs e)
        {
            string path = e.FullPath;
            string extension = Path.GetExtension(path);
            Programs.Win32 app;

            // To mitigate the issue of not having a FullPath for a shortcut app, we iterate through the items and find the app with the same hashcode.
            if (extension.Equals(lnkExtension, StringComparison.OrdinalIgnoreCase))
            {
                app = GetAppWithSameLnkResolvedPath(path);
            }
            else
            {
                app = Programs.Win32.GetAppFromPath(path);
            }

            if (app != null)
            {
                Remove(app);
            }
        }

        // To mitigate the issue faced (as stated above) when a shortcut application is renamed, the Exe FullPath and executable name must be obtained.
        // Unlike the rename event args, since we do not have a newPath, we iterate through all the programs and find the one with the same LnkResolved path.
        private Programs.Win32 GetAppWithSameLnkResolvedPath(string lnkResolvedPath)
        {
            foreach(Programs.Win32 app in Items)
            {
                if (lnkResolvedPath.ToLower().Equals(app.LnkResolvedPath))
                {
                    return app;
                }
            }
            return null;
        }

        private void OnAppCreated(object sender, FileSystemEventArgs e)
        {
            string path = e.FullPath;
            Programs.Win32 app = Programs.Win32.GetAppFromPath(path);
            if (app != null)
            {
                Add(app);
            }
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
