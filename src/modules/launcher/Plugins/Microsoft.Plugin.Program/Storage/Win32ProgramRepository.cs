// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.Abstractions;
using System.Threading.Tasks;

using Wox.Infrastructure.Storage;
using Wox.Plugin.Logger;

using Win32Program = Microsoft.Plugin.Program.Programs.Win32Program;

namespace Microsoft.Plugin.Program.Storage
{
    internal class Win32ProgramRepository : ListRepository<Programs.Win32Program>, IProgramRepository
    {
        private static readonly IFileSystem FileSystem = new FileSystem();
        private static readonly IPath Path = FileSystem.Path;

        private const string LnkExtension = ".lnk";
        private const string UrlExtension = ".url";

        private ProgramPluginSettings _settings;
        private IList<IFileSystemWatcherWrapper> _fileSystemWatcherHelpers;
        private string[] _pathsToWatch;
        private int _numberOfPathsToWatch;
        private Collection<string> extensionsToWatch = new Collection<string> { "*.exe", $"*{LnkExtension}", "*.appref-ms", $"*{UrlExtension}" };

        private static ConcurrentQueue<string> commonEventHandlingQueue = new ConcurrentQueue<string>();

        public static readonly int OnRenamedEventWaitTime = 1000;

        public Win32ProgramRepository(IList<IFileSystemWatcherWrapper> fileSystemWatcherHelpers, ProgramPluginSettings settings, string[] pathsToWatch)
        {
            _fileSystemWatcherHelpers = fileSystemWatcherHelpers;
            _settings = settings ?? throw new ArgumentNullException(nameof(settings), "Win32ProgramRepository requires an initialized settings object");
            _pathsToWatch = pathsToWatch;
            _numberOfPathsToWatch = pathsToWatch.Length;
            InitializeFileSystemWatchers();

            // This task would always run in the background trying to dequeue file paths from the queue at regular intervals.
            _ = Task.Run(async () =>
            {
                while (true)
                {
                    int dequeueDelay = 500;
                    string appPath = await EventHandler.GetAppPathFromQueueAsync(commonEventHandlingQueue, dequeueDelay).ConfigureAwait(false);

                    // To allow for the installation process to finish.
                    await Task.Delay(5000).ConfigureAwait(false);

                    if (!string.IsNullOrEmpty(appPath))
                    {
                        Programs.Win32Program app = Programs.Win32Program.GetAppFromPath(appPath);
                        if (app != null)
                        {
                            Add(app);
                        }
                    }
                }
            }).ConfigureAwait(false);
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

        private async Task DoOnAppRenamedAsync(object sender, RenamedEventArgs e)
        {
            string oldPath = e.OldFullPath;
            string newPath = e.FullPath;

            // fix for https://github.com/microsoft/PowerToys/issues/34391
            // the msi installer creates a shortcut, which is detected by the PT Run and ends up in calling this OnAppRenamed method
            // the thread needs to be halted for a short time to avoid locking the new shortcut file as we read it, otherwise the lock causes
            // in the issue scenario that a warning is popping up during the msi install process.
            await Task.Delay(OnRenamedEventWaitTime).ConfigureAwait(false);

            string extension = Path.GetExtension(newPath);
            Win32Program.ApplicationType oldAppType = Win32Program.GetAppTypeFromPath(oldPath);
            Programs.Win32Program newApp = Win32Program.GetAppFromPath(newPath);
            Programs.Win32Program oldApp = null;

            // Once the shortcut application is renamed, the old app does not exist and therefore when we try to get the FullPath we get the lnk path instead of the exe path
            // This changes the hashCode() of the old application.
            // Therefore, instead of retrieving the old app using the GetAppFromPath(), we construct the application ourself
            // This situation is not encountered for other application types because the fullPath is the path itself, instead of being computed by using the path to the app.
            try
            {
                if (oldAppType == Win32Program.ApplicationType.ShortcutApplication || oldAppType == Win32Program.ApplicationType.InternetShortcutApplication)
                {
                    oldApp = new Win32Program() { Name = Path.GetFileNameWithoutExtension(e.OldName), ExecutableName = Path.GetFileName(e.OldName), FullPath = newApp?.FullPath ?? oldPath };
                }
                else
                {
                    oldApp = Win32Program.GetAppFromPath(oldPath);
                }
            }
            catch (Exception ex)
            {
                Log.Exception($"DoOnAppRenamedAsync-{extension} Program|{e.OldName}|Unable to create program from {oldPath}", ex, GetType());
            }

            // To remove the old app which has been renamed and to add the new application.
            if (oldApp != null)
            {
                if (string.IsNullOrWhiteSpace(oldApp.Name) || string.IsNullOrWhiteSpace(oldApp.ExecutableName) || string.IsNullOrWhiteSpace(oldApp.FullPath))
                {
                    Log.Warn($"Old app data was not initialized properly for removal after file renaming. This likely means it was not a valid app to begin with and removal is not needed. OldFullPath: {e.OldFullPath}; OldName: {e.OldName}; FullPath: {e.FullPath}", GetType());
                }
                else
                {
                    Remove(oldApp);
                }
            }

            if (newApp != null)
            {
                Add(newApp);
            }
        }

        private void OnAppRenamed(object sender, RenamedEventArgs e)
        {
            Task.Run(async () =>
            {
                try
                {
                    await DoOnAppRenamedAsync(sender, e).ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    Log.Exception($"OnAppRenamed throw exception.", e, e.GetType());
                }
            }).ConfigureAwait(false);
        }

        private void OnAppDeleted(object sender, FileSystemEventArgs e)
        {
            string path = e.FullPath;
            string extension = Path.GetExtension(path);
            Programs.Win32Program app = null;

            try
            {
                // To mitigate the issue of not having a FullPath for a shortcut app, we iterate through the items and find the app with the same hashcode.
                // Using OrdinalIgnoreCase since this is used internally
                if (extension.Equals(LnkExtension, StringComparison.OrdinalIgnoreCase))
                {
                    app = GetAppWithSameLnkFilePath(path);
                    if (app == null)
                    {
                        // Cancelled links won't have a resolved path.
                        app = GetAppWithSameNameAndExecutable(Path.GetFileNameWithoutExtension(path), Path.GetFileName(path));
                    }
                }
                else if (extension.Equals(UrlExtension, StringComparison.OrdinalIgnoreCase))
                {
                    app = GetAppWithSameNameAndExecutable(Path.GetFileNameWithoutExtension(path), Path.GetFileName(path));
                }
                else
                {
                    app = Programs.Win32Program.GetAppFromPath(path);
                }
            }
            catch (Exception ex)
            {
                Log.Exception($"OnAppDeleted-{extension}Program|{path}|Unable to create program from {path}", ex, GetType());
            }

            if (app != null)
            {
                Remove(app);
            }
        }

        // When a URL application is deleted, we can no longer get the HashCode directly from the path because the FullPath a Url app is the URL obtained from reading the file
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Globalization", "CA1309:Use ordinal string comparison", Justification = "Using CurrentCultureIgnoreCase since application names could be dependent on currentculture See: https://github.com/microsoft/PowerToys/pull/5847/files#r468245190")]
        private Win32Program GetAppWithSameNameAndExecutable(string name, string executableName)
        {
            foreach (Win32Program app in Items)
            {
                // Using CurrentCultureIgnoreCase since application names could be dependent on currentculture See: https://github.com/microsoft/PowerToys/pull/5847/files#r468245190
                if (name.Equals(app.Name, StringComparison.CurrentCultureIgnoreCase) && executableName.Equals(app.ExecutableName, StringComparison.CurrentCultureIgnoreCase))
                {
                    return app;
                }
            }

            return null;
        }

        // To mitigate the issue faced (as stated above) when a shortcut application is renamed, the Exe FullPath and executable name must be obtained.
        // Unlike the rename event args, since we do not have a newPath, we iterate through all the programs and find the one with the same LnkResolved path.
        private Programs.Win32Program GetAppWithSameLnkFilePath(string lnkFilePath)
        {
            foreach (Programs.Win32Program app in Items)
            {
                // Using Invariant / OrdinalIgnoreCase since we're comparing paths
                if (lnkFilePath.ToUpperInvariant().Equals(app.LnkFilePath, StringComparison.OrdinalIgnoreCase))
                {
                    return app;
                }
            }

            return null;
        }

        private void OnAppCreated(object sender, FileSystemEventArgs e)
        {
            string path = e.FullPath;

            // Using OrdinalIgnoreCase since we're comparing extensions
            if (!Path.GetExtension(path).Equals(UrlExtension, StringComparison.OrdinalIgnoreCase) && !Path.GetExtension(path).Equals(LnkExtension, StringComparison.OrdinalIgnoreCase))
            {
                Programs.Win32Program app = Programs.Win32Program.GetAppFromPath(path);
                if (app != null)
                {
                    Add(app);
                }
            }
        }

        private void OnAppChanged(object sender, FileSystemEventArgs e)
        {
            string path = e.FullPath;

            // Using OrdinalIgnoreCase since we're comparing extensions
            if (Path.GetExtension(path).Equals(UrlExtension, StringComparison.OrdinalIgnoreCase) || Path.GetExtension(path).Equals(LnkExtension, StringComparison.OrdinalIgnoreCase))
            {
                // When a url or lnk app is installed, multiple created and changed events are triggered.
                // To prevent the code from acting on the first such event (which may still be during app installation), the events are added a common queue and dequeued by a background task at regular intervals - https://github.com/microsoft/PowerToys/issues/6429.
                commonEventHandlingQueue.Enqueue(path);
            }
        }

        public void IndexPrograms()
        {
            var applications = Programs.Win32Program.All(_settings);
            Log.Info($"Indexed {applications.Count} win32 applications", GetType());
            SetList(applications);
        }
    }
}
