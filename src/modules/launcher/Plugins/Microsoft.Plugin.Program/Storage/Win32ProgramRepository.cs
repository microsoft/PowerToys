using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Microsoft.Plugin.Program.Programs;
using Wox.Infrastructure.Logger;
using Wox.Infrastructure.Storage;
using System.IO;
using System.Linq;

namespace Microsoft.Plugin.Program.Storage
{
    using Win32 = Programs.Win32;
    internal class Win32ProgramRepository : ListRepository<Programs.Win32>, IProgramRepository
    {
        private IStorage<IList<Programs.Win32>> _storage;
        private Settings _settings;
        private IList<IFileSystemWatcherWrapper> _fileSystemWatcherHelpers;
        private string[] _pathsToWatch;
        private int _numberOfPathsToWatch;
        private Collection<string> extensionsToWatch = new Collection<string> { "*.exe", "*.lnk", "*.appref-ms", "*.url" };
        private readonly string lnkExtension = ".lnk";
        private readonly string urlExtension = ".url";

        public Win32ProgramRepository(IList<IFileSystemWatcherWrapper> fileSystemWatcherHelpers, IStorage<IList<Win32>> storage, Settings settings, string[] pathsToWatch)
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
            for (int index = 0; index < _numberOfPathsToWatch; index++)
            {
                // To set the paths to monitor
                _fileSystemWatcherHelpers[index].Path = _pathsToWatch[index];

                // to be notified when there is a change to a file
                _fileSystemWatcherHelpers[index].NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite;

                // filtering the app types that we want to monitor
                _fileSystemWatcherHelpers[index].Filters = extensionsToWatch;

                // Registering the event handlers
                _fileSystemWatcherHelpers[index].Created += OnAppCreated;
                _fileSystemWatcherHelpers[index].Deleted += OnAppDeleted;
                _fileSystemWatcherHelpers[index].Renamed += OnAppRenamed;
                _fileSystemWatcherHelpers[index].Changed += OnAppChanged;

                // Enable the file system watcher
                _fileSystemWatcherHelpers[index].EnableRaisingEvents = true;

                // Enable it to search in sub folders as well
                _fileSystemWatcherHelpers[index].IncludeSubdirectories = true;
            }
        }

        private void OnAppRenamed(object sender, RenamedEventArgs e)
        {
            string oldPath = e.OldFullPath;
            string newPath = e.FullPath;

            string extension = Path.GetExtension(newPath);
            Programs.Win32 newApp = Programs.Win32.GetAppFromPath(newPath);
            Programs.Win32 oldApp = null;

            // Once the shortcut application is renamed, the old app does not exist and therefore when we try to get the FullPath we get the lnk path instead of the exe path
            // This changes the hashCode() of the old application.
            // Therefore, instead of retrieving the old app using the GetAppFromPath(), we construct the application ourself
            // This situation is not encountered for other application types because the fullPath is the path itself, instead of being computed by using the path to the app.
            try
            {
                if (extension.Equals(lnkExtension, StringComparison.OrdinalIgnoreCase))
                {
                    oldApp = new Win32() { Name = Path.GetFileNameWithoutExtension(e.OldName), ExecutableName = newApp.ExecutableName, FullPath = newApp.FullPath };
                }
                else if (extension.Equals(urlExtension, StringComparison.OrdinalIgnoreCase))
                {
                    oldApp = new Win32() { Name = Path.GetFileNameWithoutExtension(e.OldName), ExecutableName = Path.GetFileName(e.OldName), FullPath = newApp.FullPath };
                }
                else
                {
                    oldApp = Win32.GetAppFromPath(oldPath);
                }
            }
            catch (Exception ex)
            {
                Log.Info($"|Win32ProgramRepository|OnAppRenamed-{extension}Program|{oldPath}|Unable to create program from {oldPath}| {ex.Message}");
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

        private void OnAppDeleted(object sender, FileSystemEventArgs e)
        {
            string path = e.FullPath;
            string extension = Path.GetExtension(path);
            Programs.Win32 app = null;

            try
            {
                // To mitigate the issue of not having a FullPath for a shortcut app, we iterate through the items and find the app with the same hashcode.
                if (extension.Equals(lnkExtension, StringComparison.OrdinalIgnoreCase))
                {
                    app = GetAppWithSameLnkResolvedPath(path);
                }
                else if (extension.Equals(urlExtension, StringComparison.OrdinalIgnoreCase))
                {
                    app = GetAppWithSameNameAndExecutable(Path.GetFileNameWithoutExtension(path), Path.GetFileName(path));
                }
                else
                {
                    app = Programs.Win32.GetAppFromPath(path);
                }
            }
            catch (Exception ex)
            {
                Log.Info($"|Win32ProgramRepository|OnAppDeleted-{extension}Program|{path}|Unable to create program from {path}| {ex.Message}");
            }

            if (app != null)
            {
                Remove(app);
            }
        }

        // When a URL application is deleted, we can no longer get the HashCode directly from the path because the FullPath a Url app is the URL obtained from reading the file
        private Win32 GetAppWithSameNameAndExecutable(string name, string executableName)
        {
            foreach (Win32 app in Items)
            {
                if (name.Equals(app.Name, StringComparison.OrdinalIgnoreCase) && executableName.Equals(app.ExecutableName, StringComparison.OrdinalIgnoreCase))
                {
                    return app;
                }
            }
            return null;
        }

        // To mitigate the issue faced (as stated above) when a shortcut application is renamed, the Exe FullPath and executable name must be obtained.
        // Unlike the rename event args, since we do not have a newPath, we iterate through all the programs and find the one with the same LnkResolved path.
        private Programs.Win32 GetAppWithSameLnkResolvedPath(string lnkResolvedPath)
        {
            foreach (Programs.Win32 app in Items)
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
            if (!Path.GetExtension(path).Equals(urlExtension))
            {
                Programs.Win32 app = Programs.Win32.GetAppFromPath(path);
                if (app != null)
                {
                    Add(app);
                }
            }
        }

        private void OnAppChanged(object sender, FileSystemEventArgs e)
        {
            string path = e.FullPath;
            if (Path.GetExtension(path).Equals(urlExtension))
            {
                Programs.Win32 app = Programs.Win32.GetAppFromPath(path);
                if (app != null)
                {
                    Add(app);
                }
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
