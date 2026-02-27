// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ManagedCommon;
using Microsoft.CmdPal.Ext.Apps.Programs;

namespace Microsoft.CmdPal.Ext.Apps.Storage;

[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
internal sealed partial class Win32ProgramRepository : ListRepository<Programs.Win32Program>, IProgramRepository, IDisposable
{
    private const string LnkExtension = ".lnk";
    private const string UrlExtension = ".url";
    private static readonly Collection<string> ExtensionsToWatch = ["*.exe", $"*{LnkExtension}", "*.appref-ms", $"*{UrlExtension}"];

    private AllAppsSettings _settings;
    private IList<IFileSystemWatcherWrapper> _fileSystemWatcherHelpers;
    private string[] _pathsToWatch;
    private int _numberOfPathsToWatch;

    // Snapshot of source-toggle values so we can detect when a re-index is needed.
    private bool _lastEnableStartMenu;
    private bool _lastEnableDesktop;
    private bool _lastEnableRegistry;
    private bool _lastEnablePath;

    private bool _isDirty;
    private volatile bool _isIndexing;
    private CancellationTokenSource? _indexCts;

    public bool IsIndexing => _isIndexing;

    private static ConcurrentQueue<string> commonEventHandlingQueue = [];

    public Win32ProgramRepository(IList<IFileSystemWatcherWrapper> fileSystemWatcherHelpers, AllAppsSettings settings, string[] pathsToWatch)
    {
        _fileSystemWatcherHelpers = fileSystemWatcherHelpers;
        _settings = settings ?? throw new ArgumentNullException(nameof(settings), "Win32ProgramRepository requires an initialized settings object");
        _pathsToWatch = pathsToWatch;
        _numberOfPathsToWatch = pathsToWatch.Length;
        SnapshotSourceSettings();
        InitializeFileSystemWatchers();

        _settings.Settings.SettingsChanged += (s, a) =>
        {
            if (HaveSourceSettingsChanged())
            {
                SnapshotSourceSettings();
                _indexCts?.Cancel();
                _indexCts?.Dispose();
                _indexCts = new CancellationTokenSource();
                var token = _indexCts.Token;
                _isIndexing = true;
                _isDirty = true;
                _ = Task.Run(() =>
                {
                    try
                    {
                        IndexPrograms(token);
                        _isIndexing = false;
                        _isDirty = true;
                    }
                    catch (OperationCanceledException)
                    {
                        // Superseded by a newer indexing request.
                    }
                });
            }
            else
            {
                ApplyNonAppFilter();
                _isDirty = true;
            }
        };

        // This task would always run in the background trying to dequeue file paths from the queue at regular intervals.
        _ = Task.Run(async () =>
        {
            while (true)
            {
                var dequeueDelay = 500;
                var appPath = await EventHandler.GetAppPathFromQueueAsync(commonEventHandlingQueue, dequeueDelay).ConfigureAwait(false);

                // To allow for the installation process to finish.
                await Task.Delay(5000).ConfigureAwait(false);

                if (!string.IsNullOrEmpty(appPath))
                {
                    Win32Program? app = Win32Program.GetAppFromPath(appPath);
                    if (app is not null)
                    {
                        Add(app);
                        _isDirty = true;
                    }
                }
            }
        }).ConfigureAwait(false);
    }

    public bool ShouldReload()
    {
        return _isDirty;
    }

    public void ResetReloadFlag()
    {
        _isDirty = false;
    }

    private void InitializeFileSystemWatchers()
    {
        for (var index = 0; index < _numberOfPathsToWatch; index++)
        {
            // To set the paths to monitor
            _fileSystemWatcherHelpers[index].Path = _pathsToWatch[index];

            // to be notified when there is a change to a file
            _fileSystemWatcherHelpers[index].NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite;

            // filtering the app types that we want to monitor
            _fileSystemWatcherHelpers[index].Filters = ExtensionsToWatch;

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

    private async Task OnAppRenamedAsync(object sender, RenamedEventArgs e)
    {
        var oldPath = e.OldFullPath;
        var newPath = e.FullPath;

        // fix for https://github.com/microsoft/PowerToys/issues/34391
        // the msi installer creates a shortcut, which is detected by the PT Run and ends up in calling this OnAppRenamed method
        // the thread needs to be halted for a short time to avoid locking the new shortcut file as we read it; otherwise, the lock causes
        // in the issue scenario that a warning is popping up during the msi install process.
        await Task.Delay(1000).ConfigureAwait(false);

        var extension = Path.GetExtension(newPath);
        Win32Program.ApplicationType oldAppType = Win32Program.GetAppTypeFromPath(oldPath);
        Programs.Win32Program? newApp = Win32Program.GetAppFromPath(newPath);
        Programs.Win32Program? oldApp = null;

        // Once the shortcut application is renamed, the old app does not exist and therefore when we try to get the FullPath we get the lnk path instead of the exe path
        // This changes the hashCode() of the old application.
        // Therefore, instead of retrieving the old app using the GetAppFromPath(), we construct the application ourself
        // This situation is not encountered for other application types because the fullPath is the path itself, instead of being computed by using the path to the app.
        try
        {
            if (oldAppType == Win32Program.ApplicationType.ShortcutApplication || oldAppType == Win32Program.ApplicationType.InternetShortcutApplication)
            {
                oldApp = new Win32Program() { Name = Path.GetFileNameWithoutExtension(e.OldName) ?? string.Empty, ExecutableName = Path.GetFileName(e.OldName) ?? string.Empty, FullPath = newApp?.FullPath ?? oldPath };
            }
            else
            {
                oldApp = Win32Program.GetAppFromPath(oldPath);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex.Message);
        }

        // To remove the old app which has been renamed and to add the new application.
        if (oldApp is not null)
        {
            if (string.IsNullOrWhiteSpace(oldApp.Name) || string.IsNullOrWhiteSpace(oldApp.ExecutableName) || string.IsNullOrWhiteSpace(oldApp.FullPath))
            {
            }
            else
            {
                Remove(oldApp);
                _isDirty = true;
            }
        }

        if (newApp is not null)
        {
            Add(newApp);
            _isDirty = true;
        }
    }

    private void OnAppRenamed(object sender, RenamedEventArgs e)
    {
        Task.Run(async () =>
        {
            await OnAppRenamedAsync(sender, e).ConfigureAwait(false);
        }).ConfigureAwait(false);
    }

    private void OnAppDeleted(object sender, FileSystemEventArgs e)
    {
        var path = e.FullPath;
        var extension = Path.GetExtension(path);
        Win32Program? app = null;

        try
        {
            // To mitigate the issue of not having a FullPath for a shortcut app, we iterate through the items and find the app with the same hashcode.
            // Using OrdinalIgnoreCase since this is used internally
            if (extension.Equals(LnkExtension, StringComparison.OrdinalIgnoreCase))
            {
                app = GetAppWithSameLnkFilePath(path);
                if (app is null)
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
            Logger.LogError(ex.Message);
        }

        if (app is not null)
        {
            Remove(app);
            _isDirty = true;
        }
    }

    // When a URL application is deleted, we can no longer get the HashCode directly from the path because the FullPath a Url app is the URL obtained from reading the file
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Globalization", "CA1309:Use ordinal string comparison", Justification = "Using CurrentCultureIgnoreCase since application names could be dependent on current culture See: https://github.com/microsoft/PowerToys/pull/5847/files#r468245190")]
    private Win32Program? GetAppWithSameNameAndExecutable(string name, string executableName)
    {
        foreach (Win32Program app in Items)
        {
            // Using CurrentCultureIgnoreCase since application names could be dependent on current culture See: https://github.com/microsoft/PowerToys/pull/5847/files#r468245190
            if (name.Equals(app.Name, StringComparison.CurrentCultureIgnoreCase) && executableName.Equals(app.ExecutableName, StringComparison.CurrentCultureIgnoreCase))
            {
                return app;
            }
        }

        return null;
    }

    // To mitigate the issue faced (as stated above) when a shortcut application is renamed, the Exe FullPath and executable name must be obtained.
    // Unlike the rename event args, since we do not have a newPath, we iterate through all the programs and find the one with the same LnkResolved path.
    private Programs.Win32Program? GetAppWithSameLnkFilePath(string lnkFilePath)
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
        var path = e.FullPath;

        // Using OrdinalIgnoreCase since we're comparing extensions
        if (!Path.GetExtension(path).Equals(UrlExtension, StringComparison.OrdinalIgnoreCase) && !Path.GetExtension(path).Equals(LnkExtension, StringComparison.OrdinalIgnoreCase))
        {
            Programs.Win32Program? app = Programs.Win32Program.GetAppFromPath(path);
            if (app is not null)
            {
                Add(app);
                _isDirty = true;
            }
        }
    }

    private void OnAppChanged(object sender, FileSystemEventArgs e)
    {
        var path = e.FullPath;

        // Using OrdinalIgnoreCase since we're comparing extensions
        if (Path.GetExtension(path).Equals(UrlExtension, StringComparison.OrdinalIgnoreCase) || Path.GetExtension(path).Equals(LnkExtension, StringComparison.OrdinalIgnoreCase))
        {
            // When a url or lnk app is installed, multiple created and changed events are triggered.
            // To prevent the code from acting on the first such event (which may still be during app installation), the events are added a common queue and dequeued by a background task at regular intervals - https://github.com/microsoft/PowerToys/issues/6429.
            commonEventHandlingQueue.Enqueue(path);
        }
    }

    public void IndexPrograms() => IndexPrograms(CancellationToken.None);

    public void IndexPrograms(CancellationToken cancellationToken)
    {
        var applications = Win32Program.All(_settings);
        cancellationToken.ThrowIfCancellationRequested();
        SetList(applications);
        ApplyNonAppFilter();
    }

    private void ApplyNonAppFilter()
    {
        var includeNonAppsOnDesktop = _settings.IncludeNonAppsOnDesktop;
        var includeNonAppsInStartMenu = _settings.IncludeNonAppsInStartMenu;

        foreach (var app in this)
        {
            if (app.AppType is Win32Program.ApplicationType.GenericFile or Win32Program.ApplicationType.Folder
                && !string.IsNullOrEmpty(app.LnkFilePath))
            {
                app.Enabled = (includeNonAppsOnDesktop || !app.IsInDesktop)
                           && (includeNonAppsInStartMenu || !app.IsInStartMenu);
            }
        }
    }

    private void SnapshotSourceSettings()
    {
        _lastEnableStartMenu = _settings.EnableStartMenuSource;
        _lastEnableDesktop = _settings.EnableDesktopSource;
        _lastEnableRegistry = _settings.EnableRegistrySource;
        _lastEnablePath = _settings.EnablePathEnvironmentVariableSource;
    }

    private bool HaveSourceSettingsChanged()
    {
        return _settings.EnableStartMenuSource != _lastEnableStartMenu
            || _settings.EnableDesktopSource != _lastEnableDesktop
            || _settings.EnableRegistrySource != _lastEnableRegistry
            || _settings.EnablePathEnvironmentVariableSource != _lastEnablePath;
    }

    public void Dispose()
    {
        _indexCts?.Dispose();
    }
}
