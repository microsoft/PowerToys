// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Microsoft.CmdPal.Ext.Apps.Win32;

/// <summary>Publishes current execution-alias owners without filesystem work on the search path.</summary>
public sealed partial class AppExecutionAliasCache : IDisposable
{
    public event EventHandler? Changed;

    private static readonly TimeSpan RefreshInterval = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan EventDebounceInterval = TimeSpan.FromMilliseconds(100);

    private readonly Lock _stateLock = new();
    private readonly Func<IReadOnlyDictionary<string, string>> _readOwners;
    private readonly Func<Action, Action<Exception>, IDisposable>? _createWatcher;
    private readonly TimeProvider _timeProvider;
    private readonly TimeSpan _refreshInterval;
    private readonly TimeSpan _debounceInterval;
    private readonly ILogger<AppExecutionAliasCache> _logger;
    private ImmutableDictionary<string, string> _owners = ImmutableDictionary.Create<string, string>(StringComparer.OrdinalIgnoreCase);
    private DateTimeOffset _lastRefresh = DateTimeOffset.MinValue;
    private Task _refreshTask = Task.CompletedTask;
    private IDisposable? _watcher;
    private long _watcherGeneration;
    private bool _watcherFailed;
    private bool _refreshRequested;
    private bool _debouncePending;
    private bool _refreshing;
    private bool _disposed;

    internal Task PendingRefresh
    {
        get
        {
            lock (_stateLock)
            {
                return _refreshTask;
            }
        }
    }

    /// <summary>Initializes a new instance of the <see cref="AppExecutionAliasCache"/> class. Creates a lazy cache of the current user's selected app execution aliases.</summary>
    public AppExecutionAliasCache(ILogger<AppExecutionAliasCache> logger)
        : this(ReadOwners, TimeProvider.System, RefreshInterval, logger, CreateWatcher)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="AppExecutionAliasCache"/> class. Creates a lazy alias cache with configurable ownership reads, timing, and watcher setup.</summary>
    internal AppExecutionAliasCache(
        Func<IReadOnlyDictionary<string, string>> readOwners,
        TimeProvider timeProvider,
        TimeSpan refreshInterval,
        ILogger<AppExecutionAliasCache>? logger = null,
        Func<Action, Action<Exception>, IDisposable>? createWatcher = null,
        TimeSpan? debounceInterval = null)
    {
        ArgumentNullException.ThrowIfNull(readOwners);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _readOwners = readOwners;
        _timeProvider = timeProvider;
        _refreshInterval = refreshInterval;
        _debounceInterval = debounceInterval ?? EventDebounceInterval;
        _logger = logger ?? NullLogger<AppExecutionAliasCache>.Instance;
        _createWatcher = createWatcher;
    }

    /// <summary>Returns the last immutable ownership map immediately, including during refresh.</summary>
    public ImmutableDictionary<string, string> GetSnapshot()
    {
        return Volatile.Read(ref _owners);
    }

    /// <summary>Requests lazy initialization or a background safety refresh when the cache has expired.</summary>
    public void RequestRefresh()
    {
        lock (_stateLock)
        {
            if (_disposed || _timeProvider.GetUtcNow() - _lastRefresh < _refreshInterval)
            {
                return;
            }

            QueueRefreshUnderLock(debounce: false);
        }
    }

    private void QueueRefreshUnderLock(bool debounce)
    {
        _refreshRequested = true;
        _debouncePending |= debounce;
        if (!_refreshing)
        {
            _refreshing = true;
            _refreshTask = Task.Run(RefreshLoopAsync);
        }
    }

    private async Task RefreshLoopAsync()
    {
        while (true)
        {
            var failed = false;
            var continueRefreshing = false;
            try
            {
                while (true)
                {
                    bool debounce;
                    lock (_stateLock)
                    {
                        if (_disposed || !_refreshRequested)
                        {
                            break;
                        }

                        debounce = _debouncePending;
                    }

                    EnsureWatcher();
                    if (debounce)
                    {
                        await Task.Delay(_debounceInterval, _timeProvider).ConfigureAwait(false);
                    }

                    lock (_stateLock)
                    {
                        if (_disposed)
                        {
                            break;
                        }

                        // This read covers events received during setup or debounce. Events received
                        // after this point remain pending and force another pass before the task completes.
                        _refreshRequested = _watcherFailed;
                        _debouncePending = false;
                        _lastRefresh = _timeProvider.GetUtcNow();
                    }

                    RefreshCore();
                }
            }
            catch (Exception ex)
            {
                failed = true;
                LogRefreshFailed(_logger, ex);
            }
            finally
            {
                lock (_stateLock)
                {
                    // Drain a request received while retiring in this same task. Unexpected failures
                    // release the gate without automatically retrying a potentially persistent failure.
                    continueRefreshing = !_disposed && !failed && _refreshRequested;
                    _refreshing = continueRefreshing;
                }
            }

            if (!continueRefreshing)
            {
                return;
            }
        }
    }

    private void EnsureWatcher()
    {
        if (_createWatcher is null)
        {
            return;
        }

        IDisposable? retired = null;
        long generation;
        lock (_stateLock)
        {
            if (_disposed)
            {
                return;
            }

            if (_watcherFailed)
            {
                retired = _watcher;
                _watcher = null;
                _watcherFailed = false;
                _watcherGeneration++;
                generation = 0;
            }
            else if (_watcher is not null)
            {
                return;
            }
            else
            {
                generation = ++_watcherGeneration;
            }
        }

        retired?.Dispose();
        if (generation == 0)
        {
            // Reconcile immediately, then retry monitoring on the next safety refresh.
            // Repeated watcher failures must not create an endless recovery loop.
            return;
        }

        IDisposable watcher;
        try
        {
            watcher = _createWatcher(() => OnAliasesChanged(generation), exception => OnWatcherFailed(generation, exception));
        }
        catch (Exception ex)
        {
            lock (_stateLock)
            {
                if (_watcherGeneration == generation)
                {
                    _watcherGeneration++;
                }
            }

            if (ex is not DirectoryNotFoundException)
            {
                LogWatcherSetupFailed(_logger, ex);
            }

            return;
        }

        lock (_stateLock)
        {
            if (!_disposed && generation == _watcherGeneration)
            {
                _watcher = watcher;
                return;
            }
        }

        watcher.Dispose();
    }

    private void OnAliasesChanged(long generation)
    {
        lock (_stateLock)
        {
            if (!_disposed && !_watcherFailed && generation == _watcherGeneration)
            {
                QueueRefreshUnderLock(debounce: true);
            }
        }
    }

    private void OnWatcherFailed(long generation, Exception exception)
    {
        lock (_stateLock)
        {
            if (_disposed || _watcherFailed || generation != _watcherGeneration)
            {
                return;
            }

            _watcherFailed = true;
            QueueRefreshUnderLock(debounce: true);
        }

        LogWatcherFailed(_logger, exception);
    }

    private void RefreshCore()
    {
        try
        {
            var owners = _readOwners().ToImmutableDictionary(StringComparer.OrdinalIgnoreCase);
            lock (_stateLock)
            {
                if (_disposed || HaveSameOwners(_owners, owners))
                {
                    return;
                }

                Volatile.Write(ref _owners, owners);
            }

            foreach (EventHandler handler in Changed?.GetInvocationList() ?? [])
            {
                if (Volatile.Read(ref _disposed))
                {
                    return;
                }

                try
                {
                    handler(this, EventArgs.Empty);
                }
                catch (Exception ex)
                {
                    LogListenerFailed(_logger, ex);
                }
            }
        }
        catch (Exception ex)
        {
            // Retain the previous preference through transient read failures.
            LogRefreshFailed(_logger, ex);
        }
    }

    private static bool HaveSameOwners(ImmutableDictionary<string, string> previous, ImmutableDictionary<string, string> current)
    {
        if (previous.Count != current.Count)
        {
            return false;
        }

        foreach (var entry in current)
        {
            if (!previous.TryGetValue(entry.Key, out var owner)
                || !string.Equals(owner, entry.Value, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    private static IDisposable CreateWatcher(Action changed, Action<Exception> failed)
    {
        return CreateWatcher(GetAliasesDirectory(), changed, failed);
    }

    /// <summary>Starts watching execution-alias executables and forwards changes and watcher failures.</summary>
    /// <returns>The active watcher, owned and disposed by the caller.</returns>
    internal static IDisposable CreateWatcher(string directory, Action changed, Action<Exception> failed)
    {
        if (!Directory.Exists(directory))
        {
            throw new DirectoryNotFoundException("The execution-alias directory is unavailable.");
        }

        FileSystemWatcher watcher;
        try
        {
            watcher = new FileSystemWatcher(directory, "*.exe")
            {
                IncludeSubdirectories = false,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.Attributes | NotifyFilters.LastWrite | NotifyFilters.Size,
            };
        }
        catch (ArgumentException ex) when (!Directory.Exists(directory))
        {
            // FileSystemWatcher reports a directory removed during setup as ArgumentException.
            throw new DirectoryNotFoundException("The execution-alias directory disappeared during setup.", ex);
        }

        FileSystemEventHandler onChange = (_, _) => changed();
        watcher.Created += onChange;
        watcher.Deleted += onChange;
        watcher.Changed += onChange;
        watcher.Renamed += (_, _) => changed();
        watcher.Error += (_, args) => failed(args.GetException());
        try
        {
            watcher.EnableRaisingEvents = true;
            return watcher;
        }
        catch
        {
            watcher.Dispose();
            throw;
        }
    }

    private static string GetAliasesDirectory()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrEmpty(localAppData))
        {
            throw new DirectoryNotFoundException("Local application data is unavailable.");
        }

        return Path.Combine(localAppData, "Microsoft", "WindowsApps");
    }

    private static IReadOnlyDictionary<string, string> ReadOwners()
    {
        var owners = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            foreach (var path in Directory.EnumerateFiles(GetAliasesDirectory(), "*.exe", SearchOption.TopDirectoryOnly))
            {
                try
                {
                    var alias = ReparsePoint.GetAppExecutionAliasInfo(path);
                    if (!string.IsNullOrWhiteSpace(alias?.Aumid))
                    {
                        owners[Path.GetFileName(path)] = alias.Aumid;
                    }
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    // An alias may disappear or change while Windows updates registrations.
                }
            }
        }
        catch (DirectoryNotFoundException)
        {
            // Profiles without execution aliases have no preference to apply.
        }

        return owners;
    }

    /// <summary>Stops alias watching and prevents subsequent cache refreshes.</summary>
    public void Dispose()
    {
        IDisposable? watcher;
        lock (_stateLock)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _watcherGeneration++;
            watcher = _watcher;
            _watcher = null;
            Changed = null;
        }

        watcher?.Dispose();
        GC.SuppressFinalize(this);
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Warning, Message = "Failed to refresh application execution-alias owners.")]
    private static partial void LogRefreshFailed(ILogger logger, Exception exception);

    [LoggerMessage(EventId = 2, Level = LogLevel.Warning, Message = "An application execution-alias listener failed.")]
    private static partial void LogListenerFailed(ILogger logger, Exception exception);

    [LoggerMessage(EventId = 3, Level = LogLevel.Warning, Message = "Failed to monitor application execution-alias changes; safety refresh remains available.")]
    private static partial void LogWatcherSetupFailed(ILogger logger, Exception exception);

    [LoggerMessage(EventId = 4, Level = LogLevel.Warning, Message = "Application execution-alias monitoring failed; reconciling and retrying on the next safety refresh.")]
    private static partial void LogWatcherFailed(ILogger logger, Exception exception);
}
