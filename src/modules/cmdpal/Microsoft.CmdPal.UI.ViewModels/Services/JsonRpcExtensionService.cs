// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ManagedCommon;
using Microsoft.CmdPal.Common.Services;
using Microsoft.CmdPal.UI.ViewModels.Models;
using Microsoft.CommandPalette.Extensions;
using Windows.Foundation;

namespace Microsoft.CmdPal.UI.ViewModels.Services;

/// <summary>
/// Extension service that manages JavaScript/TypeScript extensions. Each extension
/// runs as its own Node.js process communicating over JSON-RPC 2.0 via stdio.
/// The service discovers extensions in a well-known directory, watches that directory
/// for install/uninstall, and hot-reloads an extension (debounced) when its source
/// <c>*.js</c> files change.
/// </summary>
public sealed partial class JsonRpcExtensionService : IExtensionService, IJsExtensionHost, IDisposable
{
    // Consecutive crashes above this threshold disable an extension instead of restarting it.
    private const int MaxRestartAttempts = 3;

    private static readonly string ExtensionsPath = GetDefaultExtensionsPath();

    private readonly TaskScheduler _taskScheduler;
    private readonly Lock _extensionsLock = new();
    private readonly List<JSExtensionWrapper> _extensions = [];
    private readonly List<CommandProviderWrapper> _providerWrappers = [];
    private readonly HashSet<string> _disabledExtensions = new(StringComparer.Ordinal);

    // Consecutive crash-restart attempts per extension directory. Reset when an extension is
    // (re)loaded through a non-crash path (initial discovery, install, or source hot-reload).
    private readonly Dictionary<string, int> _crashCounts = new(StringComparer.OrdinalIgnoreCase);

    private readonly Lock _sourceWatcherLock = new();
    private readonly Dictionary<string, FileSystemWatcher> _sourceFileWatchers = new(StringComparer.OrdinalIgnoreCase);
    private readonly HotReloadDebouncer _hotReloadDebouncer;
    private readonly CancellationTokenSource _shutdownCts = new();

    private FileSystemWatcher? _directoryWatcher;
    private bool _disposed;

    public JsonRpcExtensionService(TaskScheduler taskScheduler)
    {
        _taskScheduler = taskScheduler;
        _hotReloadDebouncer = new HotReloadDebouncer(directory => _ = HotReloadExtensionAsync(directory));
    }

    public event TypedEventHandler<IExtensionService, IEnumerable<CommandProviderWrapper>>? OnProviderAdded;

    public event TypedEventHandler<IExtensionService, IEnumerable<CommandProviderWrapper>>? OnProviderRemoved;

    /// <summary>
    /// The action to take after an extension's Node.js process has crashed.
    /// </summary>
    internal enum CrashAction
    {
        /// <summary>Restart the extension with a fresh process and connection.</summary>
        Restart,

        /// <summary>Stop restarting the extension and leave it disabled.</summary>
        Disable,
    }

    /// <inheritdoc />
    public string ExtensionsRootPath => ExtensionsPath;

    /// <inheritdoc />
    public void StopExtension(string extensionDirectory)
    {
        if (string.IsNullOrEmpty(extensionDirectory))
        {
            return;
        }

        var removed = RemoveExtensionByDirectory(extensionDirectory);
        if (removed is not null)
        {
            OnProviderRemoved?.Invoke(this, [removed]);
        }
    }

    /// <inheritdoc />
    public bool IsExtensionDiscoverable(string extensionDirectory)
    {
        if (string.IsNullOrEmpty(extensionDirectory))
        {
            return false;
        }

        var manifestPath = Path.Combine(extensionDirectory, "package.json");
        if (!File.Exists(manifestPath))
        {
            return false;
        }

        var parseResult = JSExtensionManifest.TryParseFile(manifestPath);
        return parseResult.IsValid && parseResult.Manifest is not null;
    }

    public async Task<IEnumerable<CommandProviderWrapper>> LoadProvidersAsync(CancellationToken ct)
    {
        if (ct.IsCancellationRequested)
        {
            return [];
        }

        var sw = Stopwatch.StartNew();

        if (!EnsureExtensionsDirectory())
        {
            return [];
        }

        var wrappers = new List<CommandProviderWrapper>();
        foreach (var (directory, manifest) in DiscoverManifests(ExtensionsPath))
        {
            if (ct.IsCancellationRequested)
            {
                break;
            }

            var wrapper = await LoadExtensionAsync(directory, manifest, ct).ConfigureAwait(false);
            if (wrapper is not null)
            {
                wrappers.Add(wrapper);
            }
        }

        StartDirectoryWatcher();

        sw.Stop();
        Logger.LogInfo($"JsonRpcExtensionService: Loaded {wrappers.Count} extension(s) in {sw.ElapsedMilliseconds} ms");

        return wrappers;
    }

    public Task SignalStopAsync()
    {
        // Cancel first so any in-flight, delayed watcher handlers bail out before they
        // start an extension after we have already begun shutting down.
        CancelShutdown();

        StopDirectoryWatcher();
        StopAllSourceFileWatchers();

        List<JSExtensionWrapper> toStop;
        lock (_extensionsLock)
        {
            toStop = [.. _extensions];
            _extensions.Clear();
            _providerWrappers.Clear();
            _crashCounts.Clear();
        }

        foreach (var ext in toStop)
        {
            try
            {
                ext.ProcessExited -= OnExtensionProcessExited;
                ext.SignalDispose();
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to stop JS extension {ext.ExtensionDisplayName}: {ex.Message}");
            }
        }

        return Task.CompletedTask;
    }

    private void CancelShutdown()
    {
        try
        {
            _shutdownCts.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }
    }

    public Task<IEnumerable<IExtensionWrapper>> GetInstalledExtensionsAsync(bool includeDisabledExtensions = false)
    {
        lock (_extensionsLock)
        {
            var result = includeDisabledExtensions
                ? _extensions.Cast<IExtensionWrapper>().ToList()
                : _extensions.Where(e => !_disabledExtensions.Contains(e.ExtensionUniqueId)).Cast<IExtensionWrapper>().ToList();

            return Task.FromResult<IEnumerable<IExtensionWrapper>>(result);
        }
    }

    public async Task<IEnumerable<IExtensionWrapper>> RefreshInstalledExtensionsAsync(bool includeDisabledExtensions = false)
    {
        if (EnsureExtensionsDirectory())
        {
            foreach (var (directory, manifest) in DiscoverManifests(ExtensionsPath))
            {
                bool alreadyLoaded;
                lock (_extensionsLock)
                {
                    alreadyLoaded = _extensions.Any(e => PathsEqual(e.ManifestDirectory, directory));
                }

                if (!alreadyLoaded)
                {
                    var wrapper = await LoadExtensionAsync(directory, manifest, CancellationToken.None).ConfigureAwait(false);
                    if (wrapper is not null)
                    {
                        OnProviderAdded?.Invoke(this, [wrapper]);
                    }
                }
            }
        }

        return await GetInstalledExtensionsAsync(includeDisabledExtensions).ConfigureAwait(false);
    }

    public IExtensionWrapper? GetInstalledExtension(string extensionUniqueId)
    {
        lock (_extensionsLock)
        {
            return _extensions.FirstOrDefault(e => e.ExtensionUniqueId == extensionUniqueId);
        }
    }

    public void EnableExtension(string extensionUniqueId)
    {
        lock (_extensionsLock)
        {
            _disabledExtensions.Remove(extensionUniqueId);
        }
    }

    public void DisableExtension(string extensionUniqueId)
    {
        lock (_extensionsLock)
        {
            _disabledExtensions.Add(extensionUniqueId);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        CancelShutdown();
        StopDirectoryWatcher();
        StopAllSourceFileWatchers();
        _hotReloadDebouncer.Dispose();

        List<JSExtensionWrapper> toDispose;
        lock (_extensionsLock)
        {
            toDispose = [.. _extensions];
            _extensions.Clear();
            _providerWrappers.Clear();
            _crashCounts.Clear();
        }

        foreach (var ext in toDispose)
        {
            ext.ProcessExited -= OnExtensionProcessExited;
            ext.Dispose();
        }

        _shutdownCts.Dispose();
    }

    /// <summary>
    /// Scans <paramref name="root"/> for subdirectories that contain a package.json with a
    /// valid CmdPal manifest. Extracted as a static helper so discovery/manifest filtering
    /// can be tested without spawning Node.js processes.
    /// </summary>
    /// <param name="root">The extensions root directory to scan.</param>
    /// <returns>The valid extensions found, as (directory, manifest) pairs.</returns>
    internal static IReadOnlyList<(string Directory, JSExtensionManifest Manifest)> DiscoverManifests(string root)
    {
        var results = new List<(string, JSExtensionManifest)>();

        if (string.IsNullOrEmpty(root) || !Directory.Exists(root))
        {
            return results;
        }

        string[] subdirectories;
        try
        {
            subdirectories = Directory.GetDirectories(root);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Logger.LogError($"Failed to enumerate JS extensions in {root}: {ex.Message}");
            return results;
        }

        foreach (var subdir in subdirectories)
        {
            var manifestPath = Path.Combine(subdir, "package.json");
            if (!File.Exists(manifestPath))
            {
                continue;
            }

            var parseResult = JSExtensionManifest.TryParseFile(manifestPath);
            if (!parseResult.IsValid || parseResult.Manifest is null)
            {
                Logger.LogDebug($"Skipping {subdir}: {parseResult.FailureReason}");
                continue;
            }

            results.Add((subdir, parseResult.Manifest));
        }

        return results;
    }

    private static string GetDefaultExtensionsPath()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(localAppData, "Microsoft", "PowerToys", "CommandPalette", "JSExtensions");
    }

    private static bool PathsEqual(string a, string b) =>
        string.Equals(Path.TrimEndingDirectorySeparator(a), Path.TrimEndingDirectorySeparator(b), StringComparison.OrdinalIgnoreCase);

    private bool EnsureExtensionsDirectory()
    {
        if (Directory.Exists(ExtensionsPath))
        {
            return true;
        }

        try
        {
            Directory.CreateDirectory(ExtensionsPath);
            Logger.LogDebug($"Created JS extensions directory: {ExtensionsPath}");
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Logger.LogError($"Failed to create JS extensions directory {ExtensionsPath}: {ex.Message}");
            return false;
        }
    }

    private async Task<CommandProviderWrapper?> LoadExtensionAsync(string directory, JSExtensionManifest manifest, CancellationToken ct, bool resetCrashCount = true)
    {
        if (_disposed || _shutdownCts.IsCancellationRequested || ct.IsCancellationRequested)
        {
            return null;
        }

        JSExtensionWrapper? extensionWrapper = null;
        try
        {
            extensionWrapper = new JSExtensionWrapper(manifest, directory);

            await extensionWrapper.StartExtensionAsync().ConfigureAwait(false);

            if (!extensionWrapper.IsRunning())
            {
                Logger.LogError($"Failed to start JS extension {manifest.EffectiveDisplayName}");
                extensionWrapper.SignalDispose();
                return null;
            }

            var provider = await extensionWrapper.GetProviderAsync<ICommandProvider>().ConfigureAwait(false);
            if (provider is null)
            {
                Logger.LogWarning($"JS extension {manifest.EffectiveDisplayName} does not provide an ICommandProvider");
                extensionWrapper.SignalDispose();
                return null;
            }

            // If shutdown started while we were spawning the process, discard the new extension
            // rather than registering it after everything else has been torn down.
            if (_disposed || _shutdownCts.IsCancellationRequested || ct.IsCancellationRequested)
            {
                extensionWrapper.SignalDispose();
                return null;
            }

            var wrapper = new CommandProviderWrapper(extensionWrapper, provider, _taskScheduler);

            extensionWrapper.ProcessExited += OnExtensionProcessExited;

            lock (_extensionsLock)
            {
                _extensions.Add(extensionWrapper);
                _providerWrappers.Add(wrapper);
                if (resetCrashCount)
                {
                    _crashCounts.Remove(directory);
                }
            }

            StartSourceFileWatcher(directory);

            Logger.LogInfo($"Loaded JS extension: {manifest.EffectiveDisplayName}");
            return wrapper;
        }
        catch (Exception ex)
        {
            Logger.LogError($"Failed to load JS extension from {directory}: {ex.Message}");
            extensionWrapper?.SignalDispose();
            return null;
        }
    }

    private void OnExtensionProcessExited(object? sender, EventArgs e)
    {
        if (sender is JSExtensionWrapper wrapper)
        {
            _ = Task.Run(() => HandleExtensionCrashAsync(wrapper));
        }
    }

    private async Task HandleExtensionCrashAsync(JSExtensionWrapper wrapper)
    {
        if (_disposed || _shutdownCts.IsCancellationRequested)
        {
            return;
        }

        var directory = wrapper.ManifestDirectory;

        CommandProviderWrapper? removed = null;
        int crashCount;
        lock (_extensionsLock)
        {
            // The wrapper may already be gone (uninstall, hot-reload, or shutdown won the race).
            if (!_extensions.Remove(wrapper))
            {
                return;
            }

            removed = _providerWrappers.FirstOrDefault(w => ReferenceEquals(w.Extension, wrapper));
            if (removed is not null)
            {
                _providerWrappers.Remove(removed);
            }

            _crashCounts.TryGetValue(directory, out crashCount);
            crashCount++;
            _crashCounts[directory] = crashCount;
        }

        wrapper.ProcessExited -= OnExtensionProcessExited;

        if (removed is not null)
        {
            OnProviderRemoved?.Invoke(this, [removed]);
        }

        if (DecideCrashAction(crashCount, MaxRestartAttempts) == CrashAction.Disable)
        {
            Logger.LogError($"JS extension at {directory} crashed {crashCount} times consecutively; disabling it. Reinstall or edit the source to re-enable.");
            StopSourceFileWatcher(directory);
            return;
        }

        Logger.LogWarning($"JS extension at {directory} crashed (attempt {crashCount} of {MaxRestartAttempts}); restarting.");

        var manifestPath = Path.Combine(directory, "package.json");
        var parseResult = JSExtensionManifest.TryParseFile(manifestPath);
        if (!parseResult.IsValid || parseResult.Manifest is null)
        {
            Logger.LogError($"Cannot restart JS extension at {directory}: {parseResult.FailureReason}");
            StopSourceFileWatcher(directory);
            return;
        }

        // Preserve the crash count across the restart so repeated crashes eventually disable it.
        var restarted = await LoadExtensionAsync(directory, parseResult.Manifest, _shutdownCts.Token, resetCrashCount: false).ConfigureAwait(false);
        if (restarted is not null)
        {
            OnProviderAdded?.Invoke(this, [restarted]);
            Logger.LogInfo($"Restarted JS extension: {parseResult.Manifest.EffectiveDisplayName}");
        }
    }

    /// <summary>
    /// Decides whether an extension that has just recorded its <paramref name="crashCount"/>th
    /// consecutive crash should be restarted or disabled. Extracted as a pure function so the
    /// state transitions can be tested without spawning a Node.js process.
    /// </summary>
    /// <param name="crashCount">The consecutive crash count, already incremented for this crash.</param>
    /// <param name="maxRestartAttempts">The maximum number of restart attempts allowed.</param>
    /// <returns><see cref="CrashAction.Restart"/> while at or below the limit; otherwise <see cref="CrashAction.Disable"/>.</returns>
    internal static CrashAction DecideCrashAction(int crashCount, int maxRestartAttempts) =>
        crashCount > maxRestartAttempts ? CrashAction.Disable : CrashAction.Restart;

    private void StartDirectoryWatcher()
    {
        if (_directoryWatcher is not null || !Directory.Exists(ExtensionsPath))
        {
            return;
        }

        try
        {
            _directoryWatcher = new FileSystemWatcher(ExtensionsPath)
            {
                NotifyFilter = NotifyFilters.DirectoryName,
                EnableRaisingEvents = true,
            };

            _directoryWatcher.Created += OnExtensionDirectoryCreated;
            _directoryWatcher.Deleted += OnExtensionDirectoryDeleted;

            Logger.LogDebug($"Started directory watcher for {ExtensionsPath}");
        }
        catch (Exception ex)
        {
            Logger.LogError($"Failed to start directory watcher for {ExtensionsPath}: {ex.Message}");
        }
    }

    private void StopDirectoryWatcher()
    {
        if (_directoryWatcher is null)
        {
            return;
        }

        _directoryWatcher.Created -= OnExtensionDirectoryCreated;
        _directoryWatcher.Deleted -= OnExtensionDirectoryDeleted;
        _directoryWatcher.Dispose();
        _directoryWatcher = null;
    }

    private void OnExtensionDirectoryCreated(object sender, FileSystemEventArgs e)
    {
        var token = _shutdownCts.Token;
        _ = Task.Run(
            async () =>
            {
                try
                {
                    // Let the files finish landing before we read the manifest.
                    await Task.Delay(500, token).ConfigureAwait(false);
                }
                catch (TaskCanceledException)
                {
                    // Shutdown started while we were waiting; do not load anything.
                    return;
                }

                if (_disposed || token.IsCancellationRequested)
                {
                    return;
                }

                var manifestPath = Path.Combine(e.FullPath, "package.json");
                if (!File.Exists(manifestPath))
                {
                    return;
                }

                var parseResult = JSExtensionManifest.TryParseFile(manifestPath);
                if (!parseResult.IsValid || parseResult.Manifest is null)
                {
                    Logger.LogDebug($"Ignoring new directory {e.FullPath}: {parseResult.FailureReason}");
                    return;
                }

                bool alreadyLoaded;
                lock (_extensionsLock)
                {
                    alreadyLoaded = _extensions.Any(x => PathsEqual(x.ManifestDirectory, e.FullPath));
                }

                if (alreadyLoaded)
                {
                    return;
                }

                var wrapper = await LoadExtensionAsync(e.FullPath, parseResult.Manifest, token).ConfigureAwait(false);
                if (wrapper is not null)
                {
                    OnProviderAdded?.Invoke(this, [wrapper]);
                }
            },
            token);
    }

    private void OnExtensionDirectoryDeleted(object sender, FileSystemEventArgs e)
    {
        var removed = RemoveExtensionByDirectory(e.FullPath);
        if (removed is not null)
        {
            OnProviderRemoved?.Invoke(this, [removed]);
        }
    }

    private CommandProviderWrapper? RemoveExtensionByDirectory(string directory)
    {
        JSExtensionWrapper? extensionToRemove;
        CommandProviderWrapper? wrapperToRemove;

        lock (_extensionsLock)
        {
            extensionToRemove = _extensions.FirstOrDefault(e => PathsEqual(e.ManifestDirectory, directory));
            if (extensionToRemove is null)
            {
                return null;
            }

            _extensions.Remove(extensionToRemove);
            wrapperToRemove = _providerWrappers.FirstOrDefault(w => ReferenceEquals(w.Extension, extensionToRemove));
            if (wrapperToRemove is not null)
            {
                _providerWrappers.Remove(wrapperToRemove);
            }

            _crashCounts.Remove(directory);
        }

        extensionToRemove.ProcessExited -= OnExtensionProcessExited;
        StopSourceFileWatcher(directory);
        extensionToRemove.SignalDispose();

        return wrapperToRemove;
    }

    private void StartSourceFileWatcher(string directory)
    {
        lock (_sourceWatcherLock)
        {
            if (_sourceFileWatchers.ContainsKey(directory))
            {
                return;
            }

            try
            {
                var watcher = new FileSystemWatcher(directory, "*.js")
                {
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName,
                    IncludeSubdirectories = true,
                    EnableRaisingEvents = true,
                };

                watcher.Changed += OnSourceFileChanged;
                watcher.Created += OnSourceFileChanged;

                _sourceFileWatchers[directory] = watcher;
                Logger.LogDebug($"Started source file watcher at {directory}");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to start source file watcher at {directory}: {ex.Message}");
            }
        }
    }

    private void StopSourceFileWatcher(string directory)
    {
        lock (_sourceWatcherLock)
        {
            if (_sourceFileWatchers.TryGetValue(directory, out var watcher))
            {
                watcher.Changed -= OnSourceFileChanged;
                watcher.Created -= OnSourceFileChanged;
                watcher.Dispose();
                _sourceFileWatchers.Remove(directory);
            }
        }

        _hotReloadDebouncer.Cancel(directory);
    }

    private void StopAllSourceFileWatchers()
    {
        lock (_sourceWatcherLock)
        {
            foreach (var watcher in _sourceFileWatchers.Values)
            {
                watcher.Changed -= OnSourceFileChanged;
                watcher.Created -= OnSourceFileChanged;
                watcher.Dispose();
            }

            _sourceFileWatchers.Clear();
        }
    }

    private void OnSourceFileChanged(object sender, FileSystemEventArgs e)
    {
        if (!HotReloadDebouncer.IsRelevantChange(e.FullPath))
        {
            return;
        }

        var directory = FindWatchedDirectory(e.FullPath);
        if (directory is not null)
        {
            _hotReloadDebouncer.Notify(directory, e.FullPath);
        }
    }

    private string? FindWatchedDirectory(string changedPath)
    {
        lock (_sourceWatcherLock)
        {
            foreach (var directory in _sourceFileWatchers.Keys)
            {
                if (IsUnderDirectory(changedPath, directory))
                {
                    return directory;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Returns a value indicating whether <paramref name="path"/> is <paramref name="directory"/>
    /// itself or a descendant of it, matching only on a directory boundary. A plain prefix check
    /// would treat "foo-bar" as being under "foo"; this does not.
    /// </summary>
    /// <param name="path">The candidate path (typically a changed file).</param>
    /// <param name="directory">The directory to test containment against.</param>
    /// <returns>True when <paramref name="path"/> equals or sits under <paramref name="directory"/>.</returns>
    internal static bool IsUnderDirectory(string path, string directory)
    {
        if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(directory))
        {
            return false;
        }

        string normalizedPath;
        string normalizedDir;
        try
        {
            normalizedPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
            normalizedDir = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory));
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }

        if (normalizedPath.Length == normalizedDir.Length)
        {
            return string.Equals(normalizedPath, normalizedDir, StringComparison.OrdinalIgnoreCase);
        }

        return normalizedPath.Length > normalizedDir.Length
            && normalizedPath.StartsWith(normalizedDir, StringComparison.OrdinalIgnoreCase)
            && (normalizedPath[normalizedDir.Length] == Path.DirectorySeparatorChar
                || normalizedPath[normalizedDir.Length] == Path.AltDirectorySeparatorChar);
    }

    private async Task HotReloadExtensionAsync(string directory)
    {
        if (_disposed || _shutdownCts.IsCancellationRequested)
        {
            return;
        }

        var manifestPath = Path.Combine(directory, "package.json");
        var parseResult = JSExtensionManifest.TryParseFile(manifestPath);
        if (!parseResult.IsValid || parseResult.Manifest is null)
        {
            Logger.LogWarning($"Skipping hot-reload for {directory}: {parseResult.FailureReason}");
            return;
        }

        Logger.LogInfo($"Hot-reload: restarting {parseResult.Manifest.EffectiveDisplayName}");

        // Recreate the extension so the provider proxy rebinds to a fresh JSON-RPC connection.
        // A developer-driven source edit is a fresh start, so the crash counter is reset (this
        // happens inside RemoveExtensionByDirectory and again on the successful load below).
        var removed = RemoveExtensionByDirectory(directory);
        if (removed is not null)
        {
            OnProviderRemoved?.Invoke(this, [removed]);
        }

        var wrapper = await LoadExtensionAsync(directory, parseResult.Manifest, _shutdownCts.Token).ConfigureAwait(false);
        if (wrapper is null)
        {
            Logger.LogError($"Hot-reload failed: {parseResult.Manifest.EffectiveDisplayName} did not restart");
            return;
        }

        OnProviderAdded?.Invoke(this, [wrapper]);
        Logger.LogInfo($"Hot-reload completed for {parseResult.Manifest.EffectiveDisplayName}");
    }
}
