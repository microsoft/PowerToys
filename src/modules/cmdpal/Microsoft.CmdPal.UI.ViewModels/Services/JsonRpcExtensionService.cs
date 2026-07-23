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
/// files change.
/// </summary>
/// <remarks>
/// All lifecycle transitions for a single extension directory (initial load, refresh,
/// crash-restart, hot-reload, and removal) are serialized through a per-directory
/// <see cref="DirectoryLifecycleGate"/> so concurrent triggers can never launch
/// duplicate processes for the same extension. The synchronous <see cref="_extensionsLock"/>
/// only guards in-memory collection mutations and is never held across an await or a
/// process launch.
/// </remarks>
public sealed partial class JsonRpcExtensionService : IExtensionService, IJsExtensionHost, IDisposable
{
    // Consecutive crashes above this threshold disable an extension instead of restarting it.
    private const int MaxRestartAttempts = 3;

    // Source-file extensions that trigger a hot-reload, per the manifest contract.
    private static readonly string[] WatchedSourceExtensions = [".js", ".mjs", ".cjs"];

    // How many times a newly appeared package is re-checked for a parseable manifest
    // before giving up, and how long to wait between checks. This lets a slow install
    // (directory created first, manifest written later) settle before it is loaded.
    private const int ManifestStabilityAttempts = 20;
    private static readonly TimeSpan ManifestStabilityDelay = TimeSpan.FromMilliseconds(250);

    private static readonly string ExtensionsPath = GetDefaultExtensionsPath();

    private readonly TaskScheduler _taskScheduler;
    private readonly Lock _extensionsLock = new();
    private readonly List<JSExtensionWrapper> _extensions = [];
    private readonly List<CommandProviderWrapper> _providerWrappers = [];
    private readonly HashSet<string> _disabledExtensions = new(StringComparer.Ordinal);

    // Consecutive crash-restart attempts per canonical extension directory. Reset when
    // an extension is (re)loaded through a non-crash path (initial discovery, install,
    // or source hot-reload).
    private readonly Dictionary<string, int> _crashCounts = new(StringComparer.OrdinalIgnoreCase);

    private readonly Lock _sourceWatcherLock = new();
    private readonly Dictionary<string, FileSystemWatcher> _sourceFileWatchers = new(StringComparer.OrdinalIgnoreCase);
    private readonly HotReloadDebouncer _hotReloadDebouncer;

    // Reusable cancellation for the current load cycle. A single CancellationTokenSource
    // can only be canceled once, so stop-then-load-again would otherwise hand out a
    // permanently canceled token; this wrapper swaps in a fresh source per cycle.
    private readonly ReloadCancellation _reload = new();

    private readonly DirectoryLifecycleGate _directoryGate = new();

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

        // Route through the per-directory gate so an uninstall serializes against any
        // in-flight load, refresh, crash-restart, or hot-reload for the same directory
        // and releases every resource (wrapper, process, source watcher, crash count,
        // gate entry). The gallery calls this synchronously before deleting the
        // directory, so block until the removal completes; all awaits on this path use
        // ConfigureAwait(false) and the path is never entered from within a held gate,
        // so there is no reentrant deadlock.
        var removed = RemoveExtensionByDirectoryGatedAsync(extensionDirectory).GetAwaiter().GetResult();
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

        // Begin a fresh load cycle. This replaces a token that a previous stop left
        // canceled, so a load after a stop actually runs.
        _reload.BeginCycle();

        var sw = Stopwatch.StartNew();

        if (!EnsureExtensionsDirectory())
        {
            return [];
        }

        // Start the watcher before scanning so a package installed while the scan runs
        // is still observed (the per-directory gate and the already-loaded check make a
        // watcher-driven load and a scan-driven load for the same directory idempotent).
        StartDirectoryWatcher();

        var wrappers = new List<CommandProviderWrapper>();
        foreach (var (directory, manifest) in DiscoverAcceptedManifests(ExtensionsPath))
        {
            if (ct.IsCancellationRequested || _reload.IsStopRequested)
            {
                break;
            }

            var wrapper = await AddExtensionGatedAsync(directory, manifest, ct).ConfigureAwait(false);
            if (wrapper is not null)
            {
                wrappers.Add(wrapper);
            }
        }

        // Reconcile once more to pick up anything installed during the scan/watch gap.
        var stragglers = await AddDiscoveredNotLoadedAsync(ct).ConfigureAwait(false);
        wrappers.AddRange(stragglers);

        sw.Stop();
        Logger.LogInfo($"JsonRpcExtensionService: Loaded {wrappers.Count} extension(s) in {sw.ElapsedMilliseconds} ms");

        return wrappers;
    }

    public Task SignalStopAsync()
    {
        // Request cancellation first so any in-flight, delayed watcher handlers bail out
        // before they start an extension after we have already begun shutting down.
        _reload.Stop();

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
            // Add newly installed extensions.
            var added = await AddDiscoveredNotLoadedAsync(CancellationToken.None).ConfigureAwait(false);
            foreach (var wrapper in added)
            {
                OnProviderAdded?.Invoke(this, [wrapper]);
            }

            // Reconcile out extensions whose directory no longer exists or no longer
            // holds a valid manifest.
            var accepted = DiscoverAcceptedManifests(ExtensionsPath);
            List<string> loadedDirectories;
            lock (_extensionsLock)
            {
                loadedDirectories = _extensions.Select(e => e.ManifestDirectory).ToList();
            }

            var (_, toRemove) = ReconcileDirectories(accepted.Select(a => a.Directory), loadedDirectories);
            foreach (var directory in toRemove)
            {
                var removed = await RemoveExtensionByDirectoryGatedAsync(directory).ConfigureAwait(false);
                if (removed is not null)
                {
                    OnProviderRemoved?.Invoke(this, [removed]);
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

        _reload.Stop();
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

        _directoryGate.Dispose();
        _reload.Dispose();
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

    /// <summary>
    /// Applies the cross-extension duplicate-id policy to a discovered set: when two
    /// extensions share a normalized name key, the one whose canonical directory path
    /// sorts first (case-insensitive) wins and the rest are rejected. Sorting by path
    /// makes the winner deterministic across runs regardless of filesystem enumeration
    /// order. Extracted as a pure function so the policy can be tested directly.
    /// </summary>
    /// <param name="discovered">The discovered (directory, manifest) pairs.</param>
    /// <returns>The accepted pairs and the rejected pairs (with the winning directory).</returns>
    internal static (IReadOnlyList<(string Directory, JSExtensionManifest Manifest)> Accepted,
        IReadOnlyList<(string Directory, JSExtensionManifest Manifest, string WinnerDirectory)> Rejected)
        ResolveIdCollisions(IReadOnlyList<(string Directory, JSExtensionManifest Manifest)> discovered)
    {
        var accepted = new List<(string, JSExtensionManifest)>();
        var rejected = new List<(string, JSExtensionManifest, string)>();
        var winners = new Dictionary<string, string>(StringComparer.Ordinal);

        var ordered = discovered
            .OrderBy(d => DirectoryLifecycleGate.Canonicalize(d.Directory), StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var (directory, manifest) in ordered)
        {
            var nameKey = manifest.NameKey;
            if (string.IsNullOrEmpty(nameKey))
            {
                accepted.Add((directory, manifest));
                continue;
            }

            if (winners.TryGetValue(nameKey, out var winnerDirectory))
            {
                rejected.Add((directory, manifest, winnerDirectory));
            }
            else
            {
                winners[nameKey] = DirectoryLifecycleGate.Canonicalize(directory);
                accepted.Add((directory, manifest));
            }
        }

        return (accepted, rejected);
    }

    /// <summary>
    /// Computes the difference between what is currently discovered on disk and what is
    /// currently loaded, using canonical case-insensitive directory comparison. Extracted
    /// as a pure function so reconciliation can be tested without touching the filesystem.
    /// </summary>
    /// <param name="discovered">Directories discovered on disk.</param>
    /// <param name="loaded">Directories currently loaded by the service.</param>
    /// <returns>The directories to add (discovered but not loaded) and to remove (loaded but not discovered).</returns>
    internal static (IReadOnlyList<string> ToAdd, IReadOnlyList<string> ToRemove) ReconcileDirectories(
        IEnumerable<string> discovered,
        IEnumerable<string> loaded)
    {
        var discoveredSet = new HashSet<string>(discovered.Select(DirectoryLifecycleGate.Canonicalize), StringComparer.OrdinalIgnoreCase);
        var loadedSet = new HashSet<string>(loaded.Select(DirectoryLifecycleGate.Canonicalize), StringComparer.OrdinalIgnoreCase);

        var toAdd = discoveredSet.Where(d => !loadedSet.Contains(d)).ToList();
        var toRemove = loadedSet.Where(d => !discoveredSet.Contains(d)).ToList();

        return (toAdd, toRemove);
    }

    /// <summary>
    /// Waits for a package's manifest to become parseable, retrying a bounded number of
    /// times. This lets a slow or partially written install settle before it is loaded so
    /// it is not loaded once, failed, and then never retried. Extracted with injectable
    /// parse and delay callbacks so it can be tested deterministically.
    /// </summary>
    /// <param name="manifestPath">The package.json path to poll.</param>
    /// <param name="attempts">The maximum number of parse attempts.</param>
    /// <param name="parse">Parses the manifest at a path.</param>
    /// <param name="delay">Waits between attempts, given the zero-based attempt index.</param>
    /// <param name="ct">Cancels the wait.</param>
    /// <returns>The parsed manifest, or null if it never became valid.</returns>
    internal static async Task<JSExtensionManifest?> WaitForStableManifestAsync(
        string manifestPath,
        int attempts,
        Func<string, JSExtensionManifestParseResult> parse,
        Func<int, CancellationToken, Task> delay,
        CancellationToken ct)
    {
        for (var attempt = 0; attempt < attempts; attempt++)
        {
            if (ct.IsCancellationRequested)
            {
                return null;
            }

            var result = parse(manifestPath);
            if (result.IsValid && result.Manifest is not null)
            {
                return result.Manifest;
            }

            if (attempt < attempts - 1)
            {
                try
                {
                    await delay(attempt, ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return null;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Returns the immediate child directory of <paramref name="root"/> that contains
    /// <paramref name="fullPath"/>, i.e. the extension directory a changed path belongs
    /// to. Returns null when the path is not under the root. Extracted as a pure helper
    /// so it can be tested without a live watcher.
    /// </summary>
    /// <param name="root">The extensions root directory.</param>
    /// <param name="fullPath">A path reported by the watcher.</param>
    /// <returns>The owning extension directory, or null.</returns>
    internal static string? GetExtensionDirectoryForPath(string root, string fullPath)
    {
        if (string.IsNullOrEmpty(root) || string.IsNullOrEmpty(fullPath))
        {
            return null;
        }

        try
        {
            var normalizedRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
            var normalized = Path.TrimEndingDirectorySeparator(Path.GetFullPath(fullPath));

            var prefix = normalizedRoot + Path.DirectorySeparatorChar;
            if (!normalized.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var relative = normalized[prefix.Length..];
            var separatorIndex = relative.IndexOfAny([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar]);
            var firstSegment = separatorIndex < 0 ? relative : relative[..separatorIndex];
            if (string.IsNullOrEmpty(firstSegment))
            {
                return null;
            }

            return Path.Combine(normalizedRoot, firstSegment);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return null;
        }
    }

    private static string GetDefaultExtensionsPath()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(localAppData, "Microsoft", "PowerToys", "CmdPal", "JSExtensions");
    }

    private static bool PathsEqual(string a, string b) =>
        string.Equals(Path.TrimEndingDirectorySeparator(a), Path.TrimEndingDirectorySeparator(b), StringComparison.OrdinalIgnoreCase);

    private static string CanonicalKey(string directory) => DirectoryLifecycleGate.Canonicalize(directory);

    private static bool IsManifestPath(string path) =>
        string.Equals(Path.GetFileName(path), "package.json", StringComparison.OrdinalIgnoreCase);

    private static bool IsWatchedSourceFile(string path)
    {
        var extension = Path.GetExtension(path);
        foreach (var watched in WatchedSourceExtensions)
        {
            if (string.Equals(extension, watched, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Discovers manifests and applies the duplicate-id collision policy, logging any
    /// rejected duplicates. All full (re)load and reconciliation paths go through here so
    /// they agree on the same deterministic winner.
    /// </summary>
    private static IReadOnlyList<(string Directory, JSExtensionManifest Manifest)> DiscoverAcceptedManifests(string root)
    {
        var discovered = DiscoverManifests(root);
        var (accepted, rejected) = ResolveIdCollisions(discovered);

        foreach (var (directory, manifest, winnerDirectory) in rejected)
        {
            Logger.LogWarning(
                $"Skipping JS extension at {directory}: duplicate id '{manifest.NameKey}' is already provided by {winnerDirectory}.");
        }

        return accepted;
    }

    private bool IsStopping(CancellationToken ct) => _disposed || _reload.IsStopRequested || ct.IsCancellationRequested;

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

    /// <summary>
    /// Loads every discovered extension that is not already loaded, serialized per
    /// directory through the lifecycle gate. Returns the wrappers that were added.
    /// </summary>
    private async Task<List<CommandProviderWrapper>> AddDiscoveredNotLoadedAsync(CancellationToken ct)
    {
        var added = new List<CommandProviderWrapper>();
        var accepted = DiscoverAcceptedManifests(ExtensionsPath);

        List<string> loadedDirectories;
        lock (_extensionsLock)
        {
            loadedDirectories = _extensions.Select(e => e.ManifestDirectory).ToList();
        }

        var (toAdd, _) = ReconcileDirectories(accepted.Select(a => a.Directory), loadedDirectories);
        var toAddSet = new HashSet<string>(toAdd, StringComparer.OrdinalIgnoreCase);

        foreach (var (directory, manifest) in accepted)
        {
            if (IsStopping(ct))
            {
                break;
            }

            if (!toAddSet.Contains(DirectoryLifecycleGate.Canonicalize(directory)))
            {
                continue;
            }

            var wrapper = await AddExtensionGatedAsync(directory, manifest, ct).ConfigureAwait(false);
            if (wrapper is not null)
            {
                added.Add(wrapper);
            }
        }

        return added;
    }

    /// <summary>
    /// Adds a single extension under its per-directory gate, skipping it if it is already
    /// loaded. Serializing on the gate means a refresh, a watcher event, and a crash
    /// restart for the same directory cannot launch duplicate processes.
    /// </summary>
    private async Task<CommandProviderWrapper?> AddExtensionGatedAsync(string directory, JSExtensionManifest manifest, CancellationToken ct)
    {
        if (IsStopping(ct))
        {
            return null;
        }

        IDisposable gate;
        try
        {
            gate = await _directoryGate.AcquireAsync(directory, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (ObjectDisposedException)
        {
            return null;
        }

        using (gate)
        {
            if (IsStopping(ct))
            {
                return null;
            }

            bool alreadyLoaded;
            lock (_extensionsLock)
            {
                alreadyLoaded = _extensions.Any(e => PathsEqual(e.ManifestDirectory, directory));
            }

            if (alreadyLoaded)
            {
                return null;
            }

            return await StartAndRegisterAsync(directory, manifest, resetCrashCount: true, ct).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Starts an extension process and registers its provider. The caller must hold the
    /// directory's lifecycle gate. Any wrapper created here that cannot be registered
    /// (start failure, cancellation, or a defensive duplicate) is disposed so its process
    /// is not leaked.
    /// </summary>
    private async Task<CommandProviderWrapper?> StartAndRegisterAsync(string directory, JSExtensionManifest manifest, bool resetCrashCount, CancellationToken ct)
    {
        if (IsStopping(ct))
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

            // If shutdown started while we were spawning the process, discard the new
            // extension rather than registering it after everything else has been torn down.
            if (IsStopping(ct))
            {
                extensionWrapper.SignalDispose();
                return null;
            }

            var wrapper = new CommandProviderWrapper(extensionWrapper, provider, _taskScheduler);
            extensionWrapper.ProcessExited += OnExtensionProcessExited;

            var isDuplicate = false;
            lock (_extensionsLock)
            {
                // The per-directory gate prevents concurrent loads for one directory, but
                // keep the incumbent and drop the newcomer defensively rather than leaking
                // two live processes if a duplicate ever slips through.
                if (_extensions.Any(e => PathsEqual(e.ManifestDirectory, directory)))
                {
                    isDuplicate = true;
                }
                else
                {
                    _extensions.Add(extensionWrapper);
                    _providerWrappers.Add(wrapper);
                    if (resetCrashCount)
                    {
                        _crashCounts.Remove(CanonicalKey(directory));
                    }
                }
            }

            if (isDuplicate)
            {
                extensionWrapper.ProcessExited -= OnExtensionProcessExited;
                extensionWrapper.SignalDispose();
                return null;
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
        if (_disposed || _reload.IsStopRequested)
        {
            return;
        }

        var directory = wrapper.ManifestDirectory;

        IDisposable gate;
        try
        {
            gate = await _directoryGate.AcquireAsync(directory, _reload.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (ObjectDisposedException)
        {
            return;
        }

        using (gate)
        {
            CommandProviderWrapper? removed;
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

                var key = CanonicalKey(directory);
                _crashCounts.TryGetValue(key, out crashCount);
                crashCount++;
                _crashCounts[key] = crashCount;
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
            var restarted = await StartAndRegisterAsync(directory, parseResult.Manifest, resetCrashCount: false, _reload.Token).ConfigureAwait(false);
            if (restarted is not null)
            {
                OnProviderAdded?.Invoke(this, [restarted]);
                Logger.LogInfo($"Restarted JS extension: {parseResult.Manifest.EffectiveDisplayName}");
            }
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
                // Observe both top-level directory changes and manifest files written
                // (possibly late) inside a package, so a slow install or an atomic rename
                // promotion is still discovered.
                NotifyFilter = NotifyFilters.DirectoryName | NotifyFilters.FileName | NotifyFilters.LastWrite,
                IncludeSubdirectories = true,
                EnableRaisingEvents = true,
            };

            _directoryWatcher.Created += OnDirectoryWatcherUpsert;
            _directoryWatcher.Changed += OnDirectoryWatcherUpsert;
            _directoryWatcher.Renamed += OnDirectoryWatcherRenamed;
            _directoryWatcher.Deleted += OnDirectoryWatcherDeleted;

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

        _directoryWatcher.Created -= OnDirectoryWatcherUpsert;
        _directoryWatcher.Changed -= OnDirectoryWatcherUpsert;
        _directoryWatcher.Renamed -= OnDirectoryWatcherRenamed;
        _directoryWatcher.Deleted -= OnDirectoryWatcherDeleted;
        _directoryWatcher.Dispose();
        _directoryWatcher = null;
    }

    private void OnDirectoryWatcherUpsert(object sender, FileSystemEventArgs e)
    {
        // Only manifests and (newly created) directories drive discovery here; source
        // file edits are handled by the per-extension source watcher.
        if (IsManifestPath(e.FullPath) || Directory.Exists(e.FullPath))
        {
            HandleDirectoryEntryUpsert(e.FullPath);
        }
    }

    private void OnDirectoryWatcherRenamed(object sender, RenamedEventArgs e)
    {
        // A rename can be an atomic promotion (temp -> final) or a demotion/uninstall
        // (final -> temp). Treat the new name as a possible install and the old name as
        // a possible removal.
        if (IsManifestPath(e.FullPath) || Directory.Exists(e.FullPath))
        {
            HandleDirectoryEntryUpsert(e.FullPath);
        }

        HandleDirectoryEntryRemoved(e.OldFullPath);
    }

    private void OnDirectoryWatcherDeleted(object sender, FileSystemEventArgs e)
    {
        HandleDirectoryEntryRemoved(e.FullPath);
    }

    private void HandleDirectoryEntryUpsert(string changedPath)
    {
        var extensionDirectory = GetExtensionDirectoryForPath(ExtensionsPath, changedPath);
        if (extensionDirectory is null)
        {
            return;
        }

        var token = _reload.Token;
        _ = Task.Run(
            async () =>
            {
                var manifest = await WaitForStableManifestInstanceAsync(extensionDirectory, token).ConfigureAwait(false);
                if (manifest is null || _disposed || token.IsCancellationRequested)
                {
                    return;
                }

                bool alreadyLoaded;
                lock (_extensionsLock)
                {
                    alreadyLoaded = _extensions.Any(x => PathsEqual(x.ManifestDirectory, extensionDirectory));
                }

                if (alreadyLoaded)
                {
                    // The manifest reappeared or changed for a loaded extension: reload it
                    // so the new manifest takes effect.
                    await HotReloadExtensionAsync(extensionDirectory).ConfigureAwait(false);
                    return;
                }

                if (WouldCollideWithLoaded(extensionDirectory, manifest))
                {
                    Logger.LogWarning(
                        $"Skipping JS extension at {extensionDirectory}: an extension with id '{manifest.NameKey}' is already loaded.");
                    return;
                }

                var wrapper = await AddExtensionGatedAsync(extensionDirectory, manifest, token).ConfigureAwait(false);
                if (wrapper is not null)
                {
                    OnProviderAdded?.Invoke(this, [wrapper]);
                }
            },
            token);
    }

    private void HandleDirectoryEntryRemoved(string changedPath)
    {
        var extensionDirectory = GetExtensionDirectoryForPath(ExtensionsPath, changedPath);
        if (extensionDirectory is null)
        {
            return;
        }

        var token = _reload.Token;
        _ = Task.Run(
            async () =>
            {
                // If the extension directory still holds a valid manifest, this was not a
                // real uninstall (for example a temp file was removed); keep the extension.
                var manifestPath = Path.Combine(extensionDirectory, "package.json");
                if (Directory.Exists(extensionDirectory) && File.Exists(manifestPath))
                {
                    return;
                }

                var removed = await RemoveExtensionByDirectoryGatedAsync(extensionDirectory).ConfigureAwait(false);
                if (removed is not null)
                {
                    OnProviderRemoved?.Invoke(this, [removed]);
                }
            },
            token);
    }

    private async Task<JSExtensionManifest?> WaitForStableManifestInstanceAsync(string directory, CancellationToken ct)
    {
        var manifestPath = Path.Combine(directory, "package.json");
        return await WaitForStableManifestAsync(
            manifestPath,
            ManifestStabilityAttempts,
            JSExtensionManifest.TryParseFile,
            (_, token) => Task.Delay(ManifestStabilityDelay, token),
            ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Returns true when loading <paramref name="manifest"/> from <paramref name="directory"/>
    /// would duplicate the id of an already-loaded extension coming from a different
    /// directory. A full (re)load applies the path-sorted winner policy through
    /// <see cref="ResolveIdCollisions"/>; for a single dynamic install the already-loaded
    /// extension is kept and the newcomer is rejected.
    /// </summary>
    private bool WouldCollideWithLoaded(string directory, JSExtensionManifest manifest)
    {
        var nameKey = manifest.NameKey;
        if (string.IsNullOrEmpty(nameKey))
        {
            return false;
        }

        var canonical = DirectoryLifecycleGate.Canonicalize(directory);
        lock (_extensionsLock)
        {
            return _extensions.Any(e =>
                string.Equals(e.NameKey, nameKey, StringComparison.Ordinal) &&
                !string.Equals(DirectoryLifecycleGate.Canonicalize(e.ManifestDirectory), canonical, StringComparison.OrdinalIgnoreCase));
        }
    }

    private async Task<CommandProviderWrapper?> RemoveExtensionByDirectoryGatedAsync(string directory)
    {
        IDisposable? gate = null;
        try
        {
            gate = await _directoryGate.AcquireAsync(directory, CancellationToken.None).ConfigureAwait(false);
        }
        catch (ObjectDisposedException)
        {
            // The gate is being torn down; fall through and remove best-effort.
        }

        try
        {
            return RemoveExtensionByDirectoryCore(directory);
        }
        finally
        {
            gate?.Dispose();

            // Release the gate entry for the directory now that it is fully removed.
            _directoryGate.Remove(directory);
        }
    }

    private CommandProviderWrapper? RemoveExtensionByDirectoryCore(string directory)
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

            _crashCounts.Remove(CanonicalKey(directory));
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
                // Watch all files and filter to the source extensions in the handler so
                // that .js, .mjs, and .cjs edits all trigger a hot-reload.
                var watcher = new FileSystemWatcher(directory)
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
        if (!IsWatchedSourceFile(e.FullPath) || !HotReloadDebouncer.IsRelevantChange(e.FullPath))
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
        if (_disposed || _reload.IsStopRequested)
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

        IDisposable gate;
        try
        {
            gate = await _directoryGate.AcquireAsync(directory, _reload.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (ObjectDisposedException)
        {
            return;
        }

        using (gate)
        {
            // Recreate the extension so the provider proxy rebinds to a fresh JSON-RPC
            // connection. A developer-driven source edit is a fresh start, so the crash
            // counter is reset on the successful load below. The gate entry is preserved
            // because the directory itself is not being uninstalled.
            var removed = RemoveExtensionByDirectoryCore(directory);
            if (removed is not null)
            {
                OnProviderRemoved?.Invoke(this, [removed]);
            }

            var wrapper = await StartAndRegisterAsync(directory, parseResult.Manifest, resetCrashCount: true, _reload.Token).ConfigureAwait(false);
            if (wrapper is null)
            {
                Logger.LogError($"Hot-reload failed: {parseResult.Manifest.EffectiveDisplayName} did not restart");
                return;
            }

            OnProviderAdded?.Invoke(this, [wrapper]);
            Logger.LogInfo($"Hot-reload completed for {parseResult.Manifest.EffectiveDisplayName}");
        }
    }
}
