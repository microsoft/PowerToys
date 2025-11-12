// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.CmdPal.Ext.Apps.Programs;
using Microsoft.CmdPal.Ext.Apps.Storage;
using Microsoft.CmdPal.Ext.Apps.Utils;

namespace Microsoft.CmdPal.Ext.Apps;

public sealed partial class AppCache : IAppCache, IDisposable
{
    private Win32ProgramFileSystemWatchers _win32ProgramRepositoryHelper;

    private PackageRepository _packageRepository;

    private Win32ProgramRepository _win32ProgramRepository;

    private bool _disposed;

    public IList<Win32Program> Win32s => _win32ProgramRepository.Items;

    public IList<IUWPApplication> UWPs => _packageRepository.Items;

    public static readonly Lazy<AppCache> Instance = new(() => new());

    public AppCache()
    {
        _win32ProgramRepositoryHelper = new Win32ProgramFileSystemWatchers();

        var watchers = new List<IFileSystemWatcherWrapper>(_win32ProgramRepositoryHelper.FileSystemWatchers);

        _win32ProgramRepository = new Win32ProgramRepository(watchers, AllAppsSettings.Instance, _win32ProgramRepositoryHelper.PathsToWatch);

        _packageRepository = new PackageRepository(new PackageCatalogWrapper());

        var a = Task.Run(() =>
        {
            _win32ProgramRepository.IndexPrograms();
        });

        var b = Task.Run(() =>
        {
            _packageRepository.IndexPrograms();
            UpdateUWPIconPath(ThemeHelper.GetCurrentTheme());
        });

        try
        {
            Task.WaitAll(a, b);
        }
        catch (AggregateException ex)
        {
            ManagedCommon.Logger.LogError("One or more errors occurred while indexing apps");

            foreach (var inner in ex.InnerExceptions)
            {
                ManagedCommon.Logger.LogError(inner.Message, inner);
            }
        }

        AllAppsSettings.Instance.LastIndexTime = DateTime.Today;
    }

    private void UpdateUWPIconPath(Theme theme)
    {
        if (_packageRepository is not null)
        {
            foreach (UWPApplication app in _packageRepository)
            {
                try
                {
                    app.UpdateLogoPath(theme);
                }
                catch (Exception ex)
                {
                    ManagedCommon.Logger.LogError($"Failed to update icon path for app {app.Name}", ex);
                }
            }
        }
    }

    public bool ShouldReload() => _packageRepository.ShouldReload() || _win32ProgramRepository.ShouldReload();

    public void ResetReloadFlag()
    {
        _packageRepository.ResetReloadFlag();
        _win32ProgramRepository.ResetReloadFlag();
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                _win32ProgramRepositoryHelper?.Dispose();
                _disposed = true;
            }
        }
    }
}
