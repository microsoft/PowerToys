// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CmdPal.Common.Helpers;
using Microsoft.CmdPal.Ext.Apps.Programs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Microsoft.CmdPal.Ext.Apps.Catalog.Sources;

/// <summary>
/// Adapts one Win32 discovery origin into an independently cacheable and refreshable catalog source.
/// </summary>
internal sealed partial class Win32AppSource : IAppSource
{
    private const string CatalogSourcePrefix = "win32:";
    private const int IndexingMaxDegreeOfParallelism = 2;

    private readonly IWin32ProgramSource _source;
    private readonly string _configurationKey;
    private readonly Func<string, bool, Win32Program> _programLoader;
    private readonly ILogger<Win32AppSource> _logger;
    private readonly bool _createWatchers;
    private readonly Lock _watchersLock = new();
    private readonly List<FileSystemWatcher> _watchers = [];
    private Task? _initializationTask;
    private InterlockedBoolean _disposed;

    internal Win32AppSource(IWin32ProgramSource source, ILogger<Win32AppSource>? logger = null)
        : this(source, Win32Program.LoadFromPath, createWatchers: true, logger)
    {
    }

    internal Win32AppSource(
        IWin32ProgramSource source,
        Func<string, bool, Win32Program> programLoader,
        bool createWatchers,
        ILogger<Win32AppSource>? logger = null)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        _configurationKey = source.ConfigurationKey;
        _programLoader = programLoader ?? throw new ArgumentNullException(nameof(programLoader));
        _createWatchers = createWatchers;
        _logger = logger ?? NullLogger<Win32AppSource>.Instance;
    }

    public event EventHandler<AppSourceInvalidatedEventArgs>? Invalidated;

    public string Id => CatalogSourcePrefix + _source.Id;

    public string CacheKey => _source.CacheKey;

    /// <summary>Gets settings-derived state used by the source provider to retain unchanged instances.</summary>
    internal string ConfigurationKey => _configurationKey;

    public Task InitializeAsync(CancellationToken cancellationToken)
    {
        lock (_watchersLock)
        {
            ObjectDisposedException.ThrowIf(_disposed.Value, this);
            return _initializationTask ??= _createWatchers
                ? Task.Run(InitializeWatchers, cancellationToken)
                : Task.CompletedTask;
        }
    }

    public Task<IReadOnlyList<AppCatalogItem>> LoadAsync(CancellationToken cancellationToken)
    {
        return Task.Run<IReadOnlyList<AppCatalogItem>>(
            () => Load(cancellationToken),
            cancellationToken);
    }

    private IReadOnlyList<AppCatalogItem> Load(CancellationToken cancellationToken)
    {
        if (!_source.IsEnabled)
        {
            return [];
        }

        List<string> paths = [];
        var uniquePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in _source.GetPaths())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!string.IsNullOrWhiteSpace(path) && uniquePaths.Add(path))
            {
                paths.Add(path);
            }
        }

        var indexedItems = new ConcurrentBag<AppCatalogItem>();
        Parallel.ForEach(
            paths,
            new ParallelOptions
            {
                CancellationToken = cancellationToken,
                MaxDegreeOfParallelism = IndexingMaxDegreeOfParallelism,
            },
            path =>
            {
                var program = _programLoader(path, _source.AsRunCommand);
                if (!program.Valid)
                {
                    return;
                }

                var isNonApp = program.AppType is Win32Program.ApplicationType.GenericFile or Win32Program.ApplicationType.Folder;
                if (isNonApp && !_source.IncludeNonApps)
                {
                    return;
                }

                var identity = $"win32:{program.GetAppIdentifier()}|{program.Arguments}";
                indexedItems.Add(new AppCatalogItem(
                    identity,
                    _source.Priority,
                    new AppCatalogSourceReference(_source.Id, path),
                    CreateMatchTerms(program, path),
                    Win32AppPayload.From(program)));
            });

        var itemsByIdentity = new Dictionary<string, AppCatalogItem>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in indexedItems)
        {
            if (itemsByIdentity.TryGetValue(item.Identity, out var existing))
            {
                itemsByIdentity[item.Identity] = existing.MergeProvenance(item);
            }
            else
            {
                itemsByIdentity.Add(item.Identity, item);
            }
        }

        return new List<AppCatalogItem>(itemsByIdentity.Values);
    }

    private static List<string> CreateMatchTerms(Win32Program program, string sourcePath)
    {
        List<string> terms = [];
        var uniqueTerms = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        AddMatchTerm(terms, uniqueTerms, program.Name);
        AddMatchTerm(terms, uniqueTerms, program.NameLocalized);
        AddMatchTerm(terms, uniqueTerms, program.Description);
        AddMatchTerm(terms, uniqueTerms, program.FullPath);
        AddMatchTerm(terms, uniqueTerms, program.FullPathLocalized);
        AddMatchTerm(terms, uniqueTerms, program.ParentDirectory);
        AddMatchTerm(terms, uniqueTerms, program.ExecutableName);
        AddMatchTerm(terms, uniqueTerms, program.ExecutableNameLocalized);
        AddMatchTerm(terms, uniqueTerms, program.LnkResolvedExecutableName);
        AddMatchTerm(terms, uniqueTerms, program.LnkResolvedExecutableNameLocalized);
        AddMatchTerm(terms, uniqueTerms, sourcePath);
        return terms;
    }

    private static void AddMatchTerm(List<string> terms, HashSet<string> uniqueTerms, string? term)
    {
        if (!string.IsNullOrWhiteSpace(term) && uniqueTerms.Add(term))
        {
            terms.Add(term);
        }
    }

    /// <summary>Creates the configured Win32 discovery origins without probing their paths.</summary>
    internal static IReadOnlyList<IWin32ProgramSource> CreateProgramSources(AllAppsSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        List<IWin32ProgramSource> sources =
        [
            new StartMenuAppSource(settings),
            new DesktopAppSource(settings),
            new RegistryAppSource(settings),
            new PathEnvironmentAppSource(settings),
        ];

        foreach (var source in settings.ProgramSources)
        {
            sources.Add(new CustomDirectoryAppSource(source, settings.ProgramSuffixes));
        }

        return sources;
    }

    private void InitializeWatchers()
    {
        lock (_watchersLock)
        {
            if (_disposed.Value || !_source.IsEnabled)
            {
                return;
            }

            var watchedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var path in _source.WatchPaths)
            {
                if (!watchedPaths.Add(path))
                {
                    continue;
                }

                try
                {
                    var watcher = new FileSystemWatcher(path)
                    {
                        IncludeSubdirectories = true,
                        NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite,
                    };
                    watcher.Created += OnFileSystemChanged;
                    watcher.Deleted += OnFileSystemChanged;
                    watcher.Changed += OnFileSystemChanged;
                    watcher.Renamed += OnFileSystemRenamed;
                    watcher.Error += OnFileSystemWatcherError;
                    watcher.EnableRaisingEvents = true;
                    _watchers.Add(watcher);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
                {
                    LogWatcherSetupFailed(_logger, path, ex);
                }
            }
        }
    }

    private void OnFileSystemChanged(object sender, FileSystemEventArgs args)
    {
        if (_source.IsRelevantPath(args.FullPath))
        {
            RaiseInvalidated(
                AppSourceInvalidatedEventArgs.ForPath(
                    new AppSourcePathChange(args.ChangeType, args.FullPath)));
        }
    }

    private void OnFileSystemRenamed(object sender, RenamedEventArgs args)
    {
        if (_source.IsRelevantPath(args.FullPath) || _source.IsRelevantPath(args.OldFullPath))
        {
            RaiseInvalidated(
                AppSourceInvalidatedEventArgs.ForPath(
                    new AppSourcePathChange(WatcherChangeTypes.Renamed, args.FullPath, args.OldFullPath)));
        }
    }

    private void OnFileSystemWatcherError(object sender, ErrorEventArgs args)
    {
        LogWatcherFailed(_logger, args.GetException());
        RaiseInvalidated(AppSourceInvalidatedEventArgs.FullRefresh);
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Warning, Message = "Failed to watch application source directory '{Path}'.")]
    private static partial void LogWatcherSetupFailed(ILogger logger, string path, Exception exception);

    [LoggerMessage(EventId = 2, Level = LogLevel.Warning, Message = "An application source watcher reported an error.")]
    private static partial void LogWatcherFailed(ILogger logger, Exception exception);

    private void RaiseInvalidated(AppSourceInvalidatedEventArgs args)
    {
        if (!_disposed.Value)
        {
            Invalidated?.Invoke(this, args);
        }
    }

    public void Dispose()
    {
        if (!_disposed.Set())
        {
            return;
        }

        lock (_watchersLock)
        {
            foreach (var watcher in _watchers)
            {
                watcher.EnableRaisingEvents = false;
                watcher.Created -= OnFileSystemChanged;
                watcher.Deleted -= OnFileSystemChanged;
                watcher.Changed -= OnFileSystemChanged;
                watcher.Renamed -= OnFileSystemRenamed;
                watcher.Error -= OnFileSystemWatcherError;
                watcher.Dispose();
            }

            _watchers.Clear();
            Invalidated = null;
        }
    }
}
