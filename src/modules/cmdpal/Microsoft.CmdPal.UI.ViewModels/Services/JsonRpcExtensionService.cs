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
/// The service owns every discovered extension directory: it discovers extensions in a
/// well-known root, watches that root for install/uninstall, and maintains exactly one
/// per-extension-directory source watcher that hot-reloads an extension (debounced) when
/// its source files change.
/// </summary>
/// <remarks>
/// All lifecycle transitions for a single extension directory (initial load, refresh,
/// crash-restart, hot-reload, and removal) are serialized through a per-directory
/// <see cref="DirectoryLifecycleGate"/> so concurrent triggers can never launch
/// duplicate processes for the same extension. The synchronous <see cref="_extensionsLock"/>
/// only guards in-memory collection mutations and is never held across an await or a
/// process launch.
///
/// The same one-per-directory ownership applies to source watchers: <see cref="_extensionSourceWatchers"/>
/// holds at most one live <see cref="FileSystemWatcher"/> per extension directory, guarded by
/// <see cref="_extensionSourceWatchersLock"/>. <see cref="EnsureSourceFileWatcher"/> is the sole
/// entry point that creates or repairs one, and it is idempotent because both initial
/// registration (<see cref="StartAndRegisterAsync"/>) and hot-reload
/// (<see cref="HotReloadExtensionAsync"/>) call it to guarantee a watcher exists for the
/// directory they just (re)loaded.
///
/// Crash recovery is owned the same way, but the crash itself is established earlier than
/// the gate: <see cref="OnExtensionProcessExited"/> is the process-exit handler, so by the
/// time it runs the extension is already dead. Everything downstream of it, including the
/// directory gate acquire in <see cref="RecoverCrashedExtensionAsync"/>, is recovery rather
/// than detection; the gate only lines that recovery up behind an uninstall or hot-reload
/// already in flight for the directory. A process exit cannot be awaited by whoever raised
/// it, so recovery runs on its own task, but that task is tracked per directory by
/// <see cref="_recovery"/> instead of being detached. Uninstall
/// (<see cref="RemoveExtensionByDirectoryGatedAsync"/>), stop (<see cref="SignalStopAsync"/>),
/// and <see cref="Dispose"/> each cancel the affected recovery before awaiting it, so a
/// drain never waits on work that is blocked behind the gate the caller is about to take.
/// </remarks>
public sealed partial class JsonRpcExtensionService : IExtensionService, IJsExtensionHost, IDisposable
{
    internal const string GalleryInstallMarkerFileName = ".cmdpal-gallery-installing";

    // Consecutive crashes above this threshold disable an extension instead of restarting it.
    private const int MaxRestartAttempts = 3;

    // Source-file extensions that trigger a hot-reload, per the manifest contract.
    private static readonly string[] WatchedSourceExtensions = [".js", ".mjs", ".cjs"];

    // Path segments that never carry a relevant manifest or source change. node_modules is
    // the one directory the host still excludes unconditionally: npm writing hundreds of
    // files under it during an install is an operational hazard that causes a restart storm
    // regardless of what the manifest declares. Other directories a host might be tempted to
    // guess at (.git and other VCS metadata, build output, editor folders) are deliberately
    // not listed here; the watched scope is manifest-driven instead (see
    // <see cref="ResolveWatchRoot"/>), so those directories are excluded by not being in
    // scope rather than by the host maintaining a growing blocklist of directory names.
    private static readonly string[] IgnoredDirectorySegments = ["node_modules"];

    // How many times a newly appeared package is re-checked for a parseable manifest
    // before giving up, and how long to wait between checks. This lets a slow install
    // (directory created first, manifest written later) settle before it is loaded.
    private const int ManifestStabilityAttempts = 20;
    private static readonly TimeSpan ManifestStabilityDelay = TimeSpan.FromMilliseconds(250);
    private static readonly TimeSpan ExtensionTeardownTimeout = TimeSpan.FromSeconds(6);
    private static readonly int MaxConcurrentExtensionStarts = Math.Max(1, Math.Min(Environment.ProcessorCount, 8));

    private static readonly string ExtensionsPath = GetDefaultExtensionsPath();

    private readonly TaskScheduler _taskScheduler;

    // This gate lives for the service lifetime. Disposing it while a start owns a permit
    // could make its matching Release throw.
    private readonly SemaphoreSlim _extensionStartupGate = new(MaxConcurrentExtensionStarts, MaxConcurrentExtensionStarts);
    private readonly Lock _extensionsLock = new();
    private readonly List<JSExtensionWrapper> _extensions = [];
    private readonly List<CommandProviderWrapper> _providerWrappers = [];
    private readonly HashSet<string> _disabledExtensions = new(StringComparer.Ordinal);

    // Provider ID (normalized manifest name key) reservations shared by every
    // registration path. Consulted and claimed atomically under _extensionsLock so a
    // duplicate id can never register regardless of how it arrives (initial scan,
    // refresh, dynamic install, hot-reload, or crash-restart).
    private readonly ProviderIdReservations _providerIds = new();

    // Consecutive crash-restart attempts per canonical extension directory. Reset when
    // an extension is (re)loaded through a non-crash path (initial discovery, install,
    // or source hot-reload).
    private readonly Dictionary<string, int> _crashCounts = new(StringComparer.OrdinalIgnoreCase);

    // This service owns every extension directory it discovers, and maintains exactly one
    // source watcher per extension directory, keyed by that directory. The dictionary value
    // pairs the live FileSystemWatcher with the watch root it is currently rooted at, so
    // EnsureSourceFileWatcher can recognize when a manifest's declared watch root moved
    // (cmdpal.watchPath edited, or the entry point relocated) and repair the watcher instead
    // of silently continuing to watch a now-stale root or leaking a second watcher.
    private readonly Lock _extensionSourceWatchersLock = new();
    private readonly Dictionary<string, ExtensionSourceWatcher> _extensionSourceWatchers = new(StringComparer.OrdinalIgnoreCase);
    private readonly HotReloadDebouncer _hotReloadDebouncer;

    // Reusable cancellation for the current load cycle. A single CancellationTokenSource
    // can only be canceled once, so stop-then-load-again would otherwise hand out a
    // permanently canceled token; this wrapper swaps in a fresh source per cycle.
    private readonly ReloadCancellation _reload = new();

    private readonly DirectoryLifecycleGate _directoryGate = new();

    // Crash recovery is kicked off from a process-exit event, so the code that raises it
    // cannot await it. Every recovery task is owned by this tracker instead of a detached
    // Task.Run, keyed by extension directory, so an uninstall, a service stop, or disposal
    // can cancel it, await it, and clean it up rather than let it run on against
    // already-torn-down state.
    private readonly CrashRecoveryTracker _recovery = new();

    // Single ordered dispatch path for OnProviderAdded/OnProviderRemoved so consumers can
    // never observe a provider addition before a removal that was raised ahead of it, even
    // when the two originate on different threads.
    private readonly SerialNotificationDispatcher _notifications = new();

    private FileSystemWatcher? _directoryWatcher;
    private bool _disposed;

    // Set true, under _extensionsLock, once shutdown has cleared the collections. A start
    // that completes after this point must not register its extension (which would leak a
    // Node process and watcher past shutdown); it tears the fresh instance down instead.
    private bool _shuttingDown;
    private int _startupMarkerRecoveryCompleted;

    public JsonRpcExtensionService(TaskScheduler taskScheduler)
    {
        _taskScheduler = taskScheduler;
        _hotReloadDebouncer = new HotReloadDebouncer(directory =>
            StartObservedBackgroundTask(
                () => HotReloadExtensionAsync(directory),
                $"hot-reload JS extension at {directory}",
                _reload.Token));
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
    public async Task StopExtensionAsync(string extensionDirectory, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(extensionDirectory))
        {
            return;
        }

        // Use the lifecycle gate so uninstall waits behind any load, refresh, restart, or hot reload
        // for this directory and cleans every owned resource. The gallery calls this before deleting
        // files. Awaited work on this path uses ConfigureAwait(false), and we never enter while holding
        // the same gate, so we avoid a reentrant deadlock. The token lets Cancel stop waiting for a
        // busy gate.
        var removed = await RemoveExtensionByDirectoryGatedAsync(extensionDirectory, cancellationToken).ConfigureAwait(false);
        if (removed is not null)
        {
            RaiseProviderRemoved(removed);
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

    /// <inheritdoc />
    public bool IsExtensionInstalled(string extensionName)
    {
        if (string.IsNullOrEmpty(extensionName))
        {
            return false;
        }

        var directory = Path.Combine(ExtensionsPath, extensionName);

        // Treat a directory on disk as installed even when the provider never loaded. That lets the
        // gallery offer Uninstall for a crash disabled or corrupt install instead of stranding it.
        return IsExtensionLoadedInDirectory(directory) || IsExtensionPresentOnDisk(directory);
    }

    internal static bool IsExtensionPresentOnDisk(string extensionDirectory) =>
        !string.IsNullOrEmpty(extensionDirectory) && Directory.Exists(extensionDirectory);

    /// <inheritdoc />
    public async Task<bool> RefreshAndAwaitProviderAsync(string extensionDirectory, TimeSpan timeout, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(extensionDirectory))
        {
            return false;
        }

        if (!EnsureExtensionsDirectory())
        {
            return false;
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (timeout > TimeSpan.Zero)
        {
            timeoutCts.CancelAfter(timeout);
        }

        try
        {
            // Keep the gallery timeout scoped to the promoted extension. A slow extension
            // elsewhere should not make this install roll back.
            var added = await AddDiscoveredExtensionAsync(extensionDirectory, timeoutCts.Token).ConfigureAwait(false);
            if (added is not null)
            {
                RaiseProviderAdded(added);
            }

            if (timeoutCts.IsCancellationRequested)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return false;
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // The timeout elapsed before the extension finished loading.
            return false;
        }

        return IsExtensionLoadedInDirectory(extensionDirectory);
    }

    private bool IsExtensionLoadedInDirectory(string extensionDirectory)
    {
        lock (_extensionsLock)
        {
            return _extensions.Any(e => PathsEqual(e.ManifestDirectory, extensionDirectory));
        }
    }

    /// <summary>
    /// The result of attempting to register a freshly started extension into the service's
    /// in-memory collections under the extensions lock.
    /// </summary>
    private enum RegistrationOutcome
    {
        /// <summary>The extension was added and its provider id reserved.</summary>
        Added,

        /// <summary>Another extension is already loaded from the same directory.</summary>
        DuplicateDirectory,

        /// <summary>Another directory already owns this extension's provider id.</summary>
        DuplicateId,

        /// <summary>Shutdown began before the extension could be registered.</summary>
        Stopping,
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

        // Re-open crash recovery, which a previous stop closed, so a crash in this cycle is
        // recovered instead of being dropped as post-shutdown work.
        _recovery.BeginCycle();

        // A new load cycle clears the shutting-down guard so registrations are accepted
        // again after a previous SignalStopAsync.
        lock (_extensionsLock)
        {
            _shuttingDown = false;
        }

        var sw = Stopwatch.StartNew();

        if (!EnsureExtensionsDirectory())
        {
            return [];
        }

        RecoverStaleGalleryInstallMarkersOnce();

        // Start the watcher before scanning so installs during the scan are still observed.
        StartDirectoryWatcher();

        var accepted = DiscoverAcceptedManifests(ExtensionsPath);
        var wrappers = (await ExtensionTaskCoordinator.RunConcurrentlyAsync<(string Directory, JSExtensionManifest Manifest), CommandProviderWrapper>(
            accepted,
            item => AddExtensionGatedAsync(item.Directory, item.Manifest, ct),
            (item, ex) => Logger.LogError($"Failed to load JS extension from {item.Directory}", ex),
            MaxConcurrentExtensionStarts,
            ct)
            .ConfigureAwait(false)).ToList();

        // Reconcile once more to pick up anything installed during the scan/watch gap.
        var stragglers = await AddDiscoveredNotLoadedAsync(ct).ConfigureAwait(false);
        wrappers.AddRange(stragglers);

        sw.Stop();
        Logger.LogInfo($"JsonRpcExtensionService: Loaded {wrappers.Count} extension(s) in {sw.ElapsedMilliseconds} ms");

        return wrappers;
    }

    public async Task SignalStopAsync()
    {
        await Task.Yield();

        // Request cancellation first so any in-flight, delayed watcher handlers bail out
        // before they start an extension after we have already begun shutting down.
        _reload.Stop();

        // Close crash recovery in the same breath: cancel what is running and refuse new
        // recovery, so the process exits we are about to cause below cannot queue restart
        // work behind the shutdown.
        _recovery.CancelAll();

        StopDirectoryWatcher();
        StopAllSourceFileWatchers();

        List<JSExtensionWrapper> toStop;
        lock (_extensionsLock)
        {
            _shuttingDown = true;
            toStop = [.. _extensions];
            _extensions.Clear();
            _providerWrappers.Clear();
            _crashCounts.Clear();
            _providerIds.Clear();
        }

        await StopExtensionsConcurrentlyAsync(toStop, "stop").ConfigureAwait(false);

        // Everything was canceled above, so this only waits for already-running recovery to
        // unwind. It cannot deadlock on the directory gate: no gate is held here, and the
        // recovery tasks' tokens are already canceled.
        await _recovery.DrainAllAsync().ConfigureAwait(false);
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
            // Reconcile out extensions whose directory no longer exists or no longer
            // holds a valid manifest. Remove them before adding newly accepted extensions
            // so a duplicate ID winner can take over from the loaded loser in one refresh.
            var accepted = DiscoverAcceptedManifests(ExtensionsPath);
            List<string> loadedDirectories;
            lock (_extensionsLock)
            {
                loadedDirectories = _extensions.Select(e => e.ManifestDirectory).ToList();
            }

            var (_, toRemove) = ReconcileDirectories(accepted.Select(a => a.Directory), loadedDirectories);
            foreach (var directory in toRemove)
            {
                if (!ShouldRemoveExtensionDuringReconciliation(directory))
                {
                    continue;
                }

                var removed = await RemoveExtensionByDirectoryGatedAsync(directory).ConfigureAwait(false);
                if (removed is not null)
                {
                    RaiseProviderRemoved(removed);
                }
            }

            var added = await AddDiscoveredNotLoadedAsync(CancellationToken.None).ConfigureAwait(false);
            foreach (var wrapper in added)
            {
                RaiseProviderAdded(wrapper);
            }

            // Reload any still-present extension whose manifest changed on disk since it
            // was loaded. A plain re-enumeration only adds/removes directories, so a manifest
            // edit (new entry point, version, icon, and so on) would otherwise be ignored by
            // an explicit refresh.
            await ReloadChangedManifestsAsync(accepted).ConfigureAwait(false);
        }

        return await GetInstalledExtensionsAsync(includeDisabledExtensions).ConfigureAwait(false);
    }

    /// <summary>
    /// Compares each currently loaded extension's manifest against the accepted manifest on
    /// disk and hot-reloads any whose manifest changed. The caller passes the already
    /// discovered/accepted set so the comparison uses the same duplicate-id policy as the
    /// rest of the refresh.
    /// </summary>
    private async Task ReloadChangedManifestsAsync(
        IReadOnlyList<(string Directory, JSExtensionManifest Manifest)> accepted)
    {
        List<(string Directory, JSExtensionManifest Loaded)> loaded;
        lock (_extensionsLock)
        {
            loaded = _extensions
                .Select(e => (e.ManifestDirectory, e.Manifest))
                .ToList();
        }

        foreach (var (directory, current) in accepted)
        {
            if (IsStopping(CancellationToken.None))
            {
                break;
            }

            var match = loaded.FirstOrDefault(l => PathsEqual(l.Directory, directory));
            if (match.Loaded is null)
            {
                continue;
            }

            if (ManifestChanged(match.Loaded, current))
            {
                Logger.LogInfo($"Refresh: manifest changed for {current.EffectiveDisplayName}; reloading.");
                await HotReloadExtensionAsync(directory).ConfigureAwait(false);
            }
        }
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
        List<JSExtensionWrapper> toDispose;
        lock (_extensionsLock)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _shuttingDown = true;
            toDispose = [.. _extensions];
            _extensions.Clear();
            _providerWrappers.Clear();
            _crashCounts.Clear();
            _providerIds.Clear();
        }

        _reload.Stop();
        StopDirectoryWatcher();
        StopAllSourceFileWatchers();
        _hotReloadDebouncer.Dispose();

        // Cancel and (briefly) await crash recovery before the collections, dispatcher, and
        // directory gate it uses are torn down, so no recovery task is left running against
        // disposed state. The wait is bounded, so disposal on the UI thread cannot hang.
        _recovery.Dispose();

        StopExtensionsConcurrentlyAsync(toDispose, "dispose").GetAwaiter().GetResult();

        _notifications.Dispose();
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
    internal static IReadOnlyList<(string Directory, JSExtensionManifest Manifest)> DiscoverManifests(
        string root,
        bool includeGalleryInstalling = false)
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
            if (!includeGalleryInstalling && HasGalleryInstallMarker(subdir))
            {
                continue;
            }

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

    internal static IReadOnlyList<string> RecoverStaleGalleryInstallMarkers(
        string root,
        Action<string>? deleteMarker = null)
    {
        var failures = new List<string>();
        if (string.IsNullOrEmpty(root) || !Directory.Exists(root))
        {
            return failures;
        }

        string[] subdirectories;
        try
        {
            subdirectories = Directory.GetDirectories(root);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Logger.LogError($"Failed to enumerate stale gallery install markers in {root}: {ex.Message}");
            return failures;
        }

        deleteMarker ??= File.Delete;
        foreach (var subdirectory in subdirectories)
        {
            var markerPath = Path.Combine(subdirectory, GalleryInstallMarkerFileName);
            if (!File.Exists(markerPath))
            {
                continue;
            }

            if (!IsSafeStaleGalleryMarkerPath(root, subdirectory))
            {
                Logger.LogError($"Refusing to remove stale gallery install marker '{markerPath}' because its directory is outside the trusted extensions root or is a reparse point.");
                failures.Add(subdirectory);
                continue;
            }

            try
            {
                deleteMarker(markerPath);
                Logger.LogInfo($"Removed stale gallery install marker '{markerPath}'.");
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Logger.LogError($"Failed to remove stale gallery install marker '{markerPath}': {ex.Message}");
                failures.Add(subdirectory);
            }
        }

        return failures;
    }

    internal static bool HasGalleryInstallMarker(string extensionDirectory) =>
        File.Exists(Path.Combine(extensionDirectory, GalleryInstallMarkerFileName));

    internal static bool ShouldRemoveExtensionDuringReconciliation(string extensionDirectory) =>
        !HasGalleryInstallMarker(extensionDirectory);

    internal static bool IsSafeStaleGalleryMarkerPath(string root, string extensionDirectory)
    {
        try
        {
            var normalizedRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
            var normalizedDirectory = Path.TrimEndingDirectorySeparator(Path.GetFullPath(extensionDirectory));
            var markerPath = Path.Combine(normalizedDirectory, GalleryInstallMarkerFileName);

            return string.Equals(Path.GetDirectoryName(normalizedDirectory), normalizedRoot, StringComparison.OrdinalIgnoreCase)
                && IsUnderDirectory(markerPath, normalizedRoot)
                && (File.GetAttributes(normalizedRoot) & FileAttributes.ReparsePoint) == 0
                && (File.GetAttributes(normalizedDirectory) & FileAttributes.ReparsePoint) == 0;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException or PathTooLongException or System.Security.SecurityException)
        {
            return false;
        }
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

    /// <summary>
    /// Returns true only when a watcher change under <paramref name="root"/> is a top-level
    /// extension change: the extension directory itself (<c>&lt;root&gt;/&lt;extdir&gt;</c>) or its
    /// own manifest (<c>&lt;root&gt;/&lt;extdir&gt;/package.json</c>). Anything deeper (a nested
    /// package.json or a nested directory, for example under <c>node_modules</c> or a nested
    /// package) returns false so the recursive root watcher does not treat it as an extension
    /// upsert. Extracted as a pure helper so the depth filter can be tested without a live
    /// watcher.
    /// </summary>
    /// <param name="root">The extensions root directory.</param>
    /// <param name="fullPath">A path reported by the watcher.</param>
    /// <returns>True when the change belongs to a top-level extension entry.</returns>
    internal static bool IsTopLevelExtensionChange(string root, string fullPath)
    {
        if (string.IsNullOrEmpty(root) || string.IsNullOrEmpty(fullPath))
        {
            return false;
        }

        try
        {
            var normalizedRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
            var normalized = Path.TrimEndingDirectorySeparator(Path.GetFullPath(fullPath));

            var prefix = normalizedRoot + Path.DirectorySeparatorChar;
            if (!normalized.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var relative = normalized[prefix.Length..];
            var segments = relative.Split(
                [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                StringSplitOptions.RemoveEmptyEntries);

            return segments.Length switch
            {
                // <root>/<extdir> (the extension directory created, renamed, or removed).
                1 => true,

                // <root>/<extdir>/package.json (the extension's own manifest).
                2 => string.Equals(segments[1], "package.json", StringComparison.OrdinalIgnoreCase),

                // Anything deeper is a nested file or directory and is not an extension entry.
                _ => false,
            };
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }
    }

    private static string GetDefaultExtensionsPath()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(localAppData, "Microsoft", "PowerToys", "CmdPal", "JSExtensions");
    }

    private static bool PathsEqual(string a, string b) =>
        string.Equals(Path.TrimEndingDirectorySeparator(a), Path.TrimEndingDirectorySeparator(b), StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Decides whether a live per-extension source watcher, currently rooted at
    /// <paramref name="existingWatchRoot"/>, must be repaired (stopped and recreated at the
    /// new root) because the extension's current manifest now resolves to a different
    /// <paramref name="desiredWatchRoot"/> via <see cref="ResolveWatchRoot"/>. This is the
    /// fix for a watch root that changes while the extension is running: a hot-reloaded
    /// manifest with an edited <c>cmdpal.watchPath</c>, or one whose entry point moved to a
    /// new directory with no explicit watchPath, both produce a different desired root than
    /// the one the watcher currently observes, and either must trigger a repair rather than
    /// silently leaving the watcher pointed at a stale directory. When the roots still
    /// match, <see cref="EnsureSourceFileWatcher"/>'s ensure-call must instead be a no-op, so
    /// this is extracted as a pure helper: it isolates exactly the repair-vs-no-op decision
    /// so it can be tested without a live watcher.
    /// </summary>
    /// <param name="existingWatchRoot">The watch root the live watcher is currently rooted at.</param>
    /// <param name="desiredWatchRoot">The watch root the current manifest resolves to.</param>
    /// <returns>True when the existing watcher's root is stale and must be repaired.</returns>
    internal static bool SourceWatcherNeedsRepair(string existingWatchRoot, string desiredWatchRoot) =>
        !PathsEqual(existingWatchRoot, desiredWatchRoot);

    private static string CanonicalKey(string directory) => DirectoryLifecycleGate.Canonicalize(directory);

    /// <summary>
    /// Resolves the directory the per-extension source watcher should observe for
    /// <paramref name="directory"/>'s manifest. The manifest's declared
    /// <see cref="JSExtensionManifest.WatchDirectory"/> (cmdpal.watchPath) wins when
    /// present. Otherwise the directory containing the resolved entry point is used, so an
    /// extension that keeps its runtime output in a subfolder (for example a bundler's
    /// <c>dist/</c>) is not watched more broadly than that just because the host guessed at
    /// the whole package. Falls back to the extension directory itself only if neither can
    /// be determined, which the manifest parser does not otherwise allow. Extracted as a
    /// pure helper so the scope decision can be tested without a live watcher.
    /// </summary>
    /// <param name="directory">The extension's own directory (the watcher-ownership key).</param>
    /// <param name="manifest">The extension's parsed manifest.</param>
    /// <returns>The directory the source watcher should be rooted at.</returns>
    internal static string ResolveWatchRoot(string directory, JSExtensionManifest manifest)
    {
        if (!string.IsNullOrEmpty(manifest.WatchDirectory))
        {
            return manifest.WatchDirectory;
        }

        var entryPointDirectory = string.IsNullOrEmpty(manifest.EntryPointPath)
            ? null
            : Path.GetDirectoryName(manifest.EntryPointPath);

        return string.IsNullOrEmpty(entryPointDirectory) ? directory : entryPointDirectory;
    }

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
    /// Returns true when any directory segment of <paramref name="path"/> is one the
    /// watchers must unconditionally ignore (currently just <c>node_modules</c>, kept only
    /// to prevent a dependency-install write storm from driving discovery or hot-reload).
    /// This is a segment-aware check, so a directory named "node_modules_backup" is not
    /// matched. Other directories (VCS metadata, generated output, and so on) are excluded
    /// by watch scope (<see cref="ResolveWatchRoot"/>), not by a name added here.
    /// Extracted as a pure helper so it can be tested without a live watcher.
    /// </summary>
    /// <param name="path">The path reported by a watcher.</param>
    /// <returns>True when the path lies under an ignored directory segment.</returns>
    internal static bool HasIgnoredDirectorySegment(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return false;
        }

        var segments = path.Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries);

        foreach (var segment in segments)
        {
            foreach (var ignored in IgnoredDirectorySegments)
            {
                if (string.Equals(segment, ignored, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Returns true when a change to <paramref name="fullPath"/> should trigger a
    /// source hot-reload: it is a watched source file, is not filtered by the debouncer,
    /// and is not under an ignored directory segment. Extracted as a pure helper so the
    /// routing decision can be tested without a live watcher.
    /// </summary>
    /// <param name="fullPath">The full path of the changed source file.</param>
    /// <returns>True when the change should trigger a hot-reload.</returns>
    internal static bool ShouldReloadForSourceChange(string fullPath) =>
        !string.IsNullOrEmpty(fullPath)
        && IsWatchedSourceFile(fullPath)
        && HotReloadDebouncer.IsRelevantChange(fullPath)
        && !HasIgnoredDirectorySegment(fullPath);

    /// <summary>
    /// Discovers manifests and applies the duplicate-id collision policy, logging any
    /// rejected duplicates. All full (re)load and reconciliation paths go through here so
    /// they agree on the same deterministic winner.
    /// </summary>
    private static IReadOnlyList<(string Directory, JSExtensionManifest Manifest)> DiscoverAcceptedManifests(
        string root,
        bool includeGalleryInstalling = false)
    {
        var discovered = DiscoverManifests(root, includeGalleryInstalling);
        var (accepted, rejected) = ResolveIdCollisions(discovered);

        foreach (var (directory, manifest, winnerDirectory) in rejected)
        {
            Logger.LogWarning(
                $"Skipping JS extension at {directory}: duplicate id '{manifest.NameKey}' is already provided by {winnerDirectory}.");
        }

        return accepted;
    }

    private bool IsStopping(CancellationToken ct) => _disposed || _reload.IsStopRequested || ct.IsCancellationRequested;

    private void RecoverStaleGalleryInstallMarkersOnce()
    {
        if (Interlocked.Exchange(ref _startupMarkerRecoveryCompleted, 1) == 0)
        {
            RecoverStaleGalleryInstallMarkers(ExtensionsPath);
        }
    }

    // Provider add/remove notifications are raised through a single ordered dispatcher so a
    // consumer never observes an addition ahead of a removal that was raised before it.
    private void RaiseProviderAdded(CommandProviderWrapper wrapper) =>
        _notifications.Enqueue(() => OnProviderAdded?.Invoke(this, [wrapper]));

    private void RaiseProviderRemoved(CommandProviderWrapper wrapper) =>
        _notifications.Enqueue(() => OnProviderRemoved?.Invoke(this, [wrapper]));

    // A swap (hot-reload or crash-restart) raises the removal of the old provider and the
    // addition of the new one as one enqueued action so the pair can never be split, nor
    // observed out of order, by another operation's emission.
    private void RaiseProviderSwapped(CommandProviderWrapper? removed, CommandProviderWrapper added) =>
        _notifications.Enqueue(() =>
        {
            if (removed is not null)
            {
                OnProviderRemoved?.Invoke(this, [removed]);
            }

            OnProviderAdded?.Invoke(this, [added]);
        });

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
        var accepted = DiscoverAcceptedManifests(ExtensionsPath);

        List<string> loadedDirectories;
        lock (_extensionsLock)
        {
            loadedDirectories = _extensions.Select(e => e.ManifestDirectory).ToList();
        }

        var (toAdd, _) = ReconcileDirectories(accepted.Select(a => a.Directory), loadedDirectories);
        var toAddSet = new HashSet<string>(toAdd, StringComparer.OrdinalIgnoreCase);

        var candidates = accepted.Where(item =>
            !IsStopping(ct) &&
            toAddSet.Contains(DirectoryLifecycleGate.Canonicalize(item.Directory)));

        var added = await ExtensionTaskCoordinator.RunConcurrentlyAsync<(string Directory, JSExtensionManifest Manifest), CommandProviderWrapper>(
            candidates,
            item => AddExtensionGatedAsync(item.Directory, item.Manifest, ct),
            (item, ex) => Logger.LogError($"Failed to load JS extension from {item.Directory}", ex),
            MaxConcurrentExtensionStarts,
            ct)
            .ConfigureAwait(false);

        return [.. added];
    }

    private async Task<CommandProviderWrapper?> AddDiscoveredExtensionAsync(string extensionDirectory, CancellationToken ct)
    {
        var candidate = FindManifestByDirectory(
            DiscoverAcceptedManifests(ExtensionsPath, includeGalleryInstalling: true),
            extensionDirectory);
        if (candidate is null)
        {
            return null;
        }

        return await AddExtensionGatedAsync(candidate.Value.Directory, candidate.Value.Manifest, ct).ConfigureAwait(false);
    }

    internal static (string Directory, JSExtensionManifest Manifest)? FindManifestByDirectory(
        IReadOnlyList<(string Directory, JSExtensionManifest Manifest)> manifests,
        string extensionDirectory)
    {
        foreach (var manifest in manifests)
        {
            if (PathsEqual(manifest.Directory, extensionDirectory))
            {
                return manifest;
            }
        }

        return null;
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
        catch (OperationCanceledException) when (_disposed || _reload.IsStopRequested)
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
    /// Starts an extension process and returns a started-but-unregistered instance, or null
    /// if the process could not start or does not provide an <see cref="ICommandProvider"/>.
    /// The caller must hold the directory's lifecycle gate. This does not mutate the
    /// service collections or reserve a provider id; it is the validate half of a
    /// validate-then-swap so a hot-reload can start a replacement before removing the
    /// incumbent. Any wrapper created here that fails validation is disposed so its process
    /// is not leaked.
    /// </summary>
    private async Task<StartedInstance?> StartInstanceAsync(string directory, JSExtensionManifest manifest, CancellationToken ct)
    {
        if (IsStopping(ct))
        {
            return null;
        }

        try
        {
            return await ExtensionTaskCoordinator.RunWithConcurrencyLimitAsync(
                _extensionStartupGate,
                () => StartInstanceWithinStartupSlotAsync(directory, manifest, ct),
                ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return null;
        }
    }

    private async Task<StartedInstance?> StartInstanceWithinStartupSlotAsync(string directory, JSExtensionManifest manifest, CancellationToken ct)
    {
        if (IsStopping(ct))
        {
            return null;
        }

        JSExtensionWrapper? extensionWrapper = null;
        try
        {
            extensionWrapper = new JSExtensionWrapper(manifest, directory);

            await extensionWrapper.StartExtensionAsync(ct).ConfigureAwait(false);

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

            var wrapper = CommandProviderWrapper.CreateForJsonRpcExtension(extensionWrapper, provider, _taskScheduler);
            return new StartedInstance(extensionWrapper, wrapper);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            extensionWrapper?.SignalDispose();
            throw;
        }
        catch (Exception ex)
        {
            Logger.LogError($"Failed to load JS extension from {directory}: {ex.Message}");
            extensionWrapper?.SignalDispose();
            return null;
        }
    }

    /// <summary>
    /// Starts an extension process and registers its provider. The caller must hold the
    /// directory's lifecycle gate. Any wrapper created here that cannot be registered
    /// (start failure, cancellation, shutdown, or a defensive duplicate) is disposed so its
    /// process is not leaked.
    /// </summary>
    private async Task<CommandProviderWrapper?> StartAndRegisterAsync(string directory, JSExtensionManifest manifest, bool resetCrashCount, CancellationToken ct)
    {
        var instance = await StartInstanceAsync(directory, manifest, ct).ConfigureAwait(false);
        if (instance is null)
        {
            return null;
        }

        var extensionWrapper = instance.Extension;
        var wrapper = instance.Wrapper;
        extensionWrapper.ProcessExited += OnExtensionProcessExited;

        var outcome = RegistrationOutcome.Added;
        lock (_extensionsLock)
        {
            // Shutdown cleared the collections while this process was starting; do not
            // register it, or its Node process and watcher would leak past shutdown.
            if (_shuttingDown)
            {
                outcome = RegistrationOutcome.Stopping;
            }

            // The per-directory gate prevents concurrent loads for one directory, but
            // keep the incumbent and drop the newcomer defensively rather than leaking
            // two live processes if a duplicate ever slips through.
            else if (_extensions.Any(e => PathsEqual(e.ManifestDirectory, directory)))
            {
                outcome = RegistrationOutcome.DuplicateDirectory;
            }
            else if (!_providerIds.TryReserve(extensionWrapper.NameKey, CanonicalKey(directory)))
            {
                // Another directory already owns this provider id. Claiming the id and
                // adding to _extensions happen as one atomic step under this lock, so no
                // interleaving install, hot-reload, or crash-restart can register a
                // second provider with the same id.
                outcome = RegistrationOutcome.DuplicateId;
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

        if (outcome != RegistrationOutcome.Added)
        {
            if (outcome == RegistrationOutcome.DuplicateId)
            {
                Logger.LogWarning(
                    $"Skipping JS extension at {directory}: provider id '{extensionWrapper.NameKey}' is already reserved by another extension.");
            }

            extensionWrapper.ProcessExited -= OnExtensionProcessExited;
            extensionWrapper.SignalDispose();
            return null;
        }

        EnsureSourceFileWatcher(directory, manifest);

        // A process can exit immediately after init (for example a provider that faults
        // on first use). If that exit fired before we subscribed to ProcessExited above,
        // the event was missed; detect the dead process here and drive the same crash
        // path so an immediate post-init crash is handled (restart or disable) instead
        // of being registered as healthy. The handler runs on a separate, tracked recovery
        // task so it acquires the directory gate only after this registration releases it,
        // and it is idempotent, so racing the real event is harmless.
        if (!extensionWrapper.IsRunning())
        {
            OnExtensionProcessExited(extensionWrapper, EventArgs.Empty);
        }

        Logger.LogInfo($"Loaded JS extension: {manifest.EffectiveDisplayName}");
        return wrapper;
    }

    /// <summary>
    /// An extension that has started and validated (its process is running and it provides
    /// an <see cref="ICommandProvider"/>) but has not yet been registered into the service
    /// collections. Used by the validate-then-swap hot-reload path.
    /// </summary>
    private sealed record StartedInstance(JSExtensionWrapper Extension, CommandProviderWrapper Wrapper);

    /// <summary>
    /// The process-exit event is where a crash is established: by the time this handler
    /// runs, the extension's process is already gone. Everything from here on, including
    /// <see cref="RecoverCrashedExtensionAsync"/> and the gate it acquires, is recovering
    /// from a crash that already happened, not deciding whether one did.
    /// </summary>
    private void OnExtensionProcessExited(object? sender, EventArgs e)
    {
        if (sender is not JSExtensionWrapper wrapper)
        {
            return;
        }

        // Recovery runs on its own task (it has to: this can be raised from inside the
        // directory gate that recovery itself needs), but it is owned by the tracker rather
        // than detached. A rejected track means the service is stopping or this directory is
        // being uninstalled, in which case there is nothing left to restart.
        if (!_recovery.TryTrack(wrapper.ManifestDirectory, ct => RecoverCrashedExtensionAsync(wrapper, ct)))
        {
            Logger.LogDebug(
                $"Skipping crash recovery for {wrapper.ManifestDirectory}: the service is stopping or the directory is being removed.");
        }
    }

    /// <summary>
    /// Recovers from a crash that <see cref="OnExtensionProcessExited"/> already established.
    /// The directory gate acquired below does not confirm or decide the crash; it only
    /// queues this recovery behind an uninstall or hot-reload already running for the
    /// directory, the same as every other lifecycle operation on it.
    /// </summary>
    private async Task RecoverCrashedExtensionAsync(JSExtensionWrapper wrapper, CancellationToken ct)
    {
        if (IsStopping(ct))
        {
            return;
        }

        var directory = wrapper.ManifestDirectory;

        IDisposable gate;
        try
        {
            // Wait our turn behind any uninstall or hot-reload already running for this
            // directory. The crash is already a fact; this only serializes what we do about it.
            gate = await _directoryGate.AcquireAsync(directory, ct).ConfigureAwait(false);
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
            // An uninstall, stop, or disposal that started while this was queued behind the
            // gate owns the teardown now; leave the extension to it instead of restarting
            // something that is on its way out.
            if (IsStopping(ct))
            {
                return;
            }

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

                // Free the provider id as part of the same atomic removal so a different
                // extension can claim it, and so the restart below can re-reserve it.
                _providerIds.Release(wrapper.NameKey, key);
            }

            wrapper.ProcessExited -= OnExtensionProcessExited;

            if (removed is not null)
            {
                RaiseProviderRemoved(removed);
            }

            if (DecideCrashAction(crashCount, MaxRestartAttempts) == CrashAction.Disable)
            {
                Logger.LogError($"JS extension at {directory} crashed {crashCount} times consecutively; disabling it. Edit the source or reinstall to re-enable.");

                // Keep the source-file watcher alive so a developer source edit fires a
                // hot-reload, which resets the crash count and retries the load. Stopping it
                // here would strand the extension disabled until a full reinstall.
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
            var restarted = await StartAndRegisterAsync(directory, parseResult.Manifest, resetCrashCount: false, ct).ConfigureAwait(false);
            if (restarted is not null)
            {
                RaiseProviderAdded(restarted);
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

    /// <summary>
    /// Returns true when the salient fields of <paramref name="current"/> differ from
    /// <paramref name="loaded"/>, i.e. an edit to the manifest that would change how the
    /// extension runs or presents. Extracted as a pure function so an explicit refresh can
    /// decide to reload a changed manifest without touching the filesystem in tests.
    /// </summary>
    /// <param name="loaded">The manifest the extension is currently running with.</param>
    /// <param name="current">The manifest as it now exists on disk.</param>
    /// <returns>True when the manifest changed in a way that warrants a reload.</returns>
    internal static bool ManifestChanged(JSExtensionManifest loaded, JSExtensionManifest current)
    {
        if (loaded is null || current is null)
        {
            return false;
        }

        return !string.Equals(loaded.Name, current.Name, StringComparison.Ordinal)
            || !string.Equals(loaded.DisplayName, current.DisplayName, StringComparison.Ordinal)
            || !string.Equals(loaded.Version, current.Version, StringComparison.Ordinal)
            || !string.Equals(loaded.Description, current.Description, StringComparison.Ordinal)
            || !string.Equals(loaded.Icon, current.Icon, StringComparison.Ordinal)
            || !string.Equals(loaded.Publisher, current.Publisher, StringComparison.Ordinal)
            || !string.Equals(loaded.Main, current.Main, StringComparison.Ordinal)
            || !string.Equals(loaded.EntryPointPath, current.EntryPointPath, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(loaded.WatchDirectory, current.WatchDirectory, StringComparison.OrdinalIgnoreCase)
            || loaded.Debug != current.Debug
            || loaded.DebugPort != current.DebugPort;
    }

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

                // A recursive watch over the extensions root (which contains each
                // extension's node_modules tree) can burst well past the default 8 KB
                // buffer during an install. Enlarge it to make an overflow far less
                // likely; the Error handler recovers if one still happens.
                InternalBufferSize = 64 * 1024,
            };

            // Attach handlers before enabling events so a change that lands in the
            // window between construction and subscription is not dropped.
            _directoryWatcher.Created += OnDirectoryWatcherUpsert;
            _directoryWatcher.Changed += OnDirectoryWatcherUpsert;
            _directoryWatcher.Renamed += OnDirectoryWatcherRenamed;
            _directoryWatcher.Deleted += OnDirectoryWatcherDeleted;
            _directoryWatcher.Error += OnDirectoryWatcherError;

            _directoryWatcher.EnableRaisingEvents = true;

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
        _directoryWatcher.Error -= OnDirectoryWatcherError;
        _directoryWatcher.Dispose();
        _directoryWatcher = null;
    }

    private void OnDirectoryWatcherUpsert(object sender, FileSystemEventArgs e)
    {
        // Ignore churn under node_modules (npm writing many package.json files during an
        // install) so it cannot drive a discovery or hot-reload storm.
        if (HasIgnoredDirectorySegment(e.FullPath))
        {
            return;
        }

        // The root watcher is recursive, so it also reports nested files and directories.
        // Only a top-level <root>/<extdir> directory or its own <root>/<extdir>/package.json
        // is an extension change; a nested package.json or directory (a nested package or
        // dependency) must not be treated as an extension upsert.
        if (!IsTopLevelExtensionChange(ExtensionsPath, e.FullPath))
        {
            return;
        }

        // Only manifests and (newly created) directories drive discovery here; source
        // file edits are handled by the per-extension source watcher.
        if (IsManifestPath(e.FullPath) || Directory.Exists(e.FullPath))
        {
            HandleDirectoryEntryUpsert(e.FullPath);
        }
    }

    private void OnDirectoryWatcherRenamed(object sender, RenamedEventArgs e)
    {
        // A rename can be an atomic promotion from temp to final, or a demotion or uninstall
        // from final to temp. Treat the new name as a possible install and the old name as
        // a possible removal, ignoring either side that sits under an ignored segment.
        // The new name must also be a top-level extension entry (directory or its own
        // manifest); a nested rename is not an extension change.
        if (!HasIgnoredDirectorySegment(e.FullPath)
            && IsTopLevelExtensionChange(ExtensionsPath, e.FullPath)
            && (IsManifestPath(e.FullPath) || Directory.Exists(e.FullPath)))
        {
            HandleDirectoryEntryUpsert(e.FullPath);
        }

        if (ShouldRouteDirectoryRemoval(ExtensionsPath, e.OldFullPath))
        {
            HandleDirectoryEntryRemoved(e.OldFullPath);
        }
    }

    private void OnDirectoryWatcherDeleted(object sender, FileSystemEventArgs e)
    {
        if (!ShouldRouteDirectoryRemoval(ExtensionsPath, e.FullPath))
        {
            return;
        }

        HandleDirectoryEntryRemoved(e.FullPath);
    }

    internal static bool ShouldRouteDirectoryRemoval(string root, string fullPath)
    {
        return !HasIgnoredDirectorySegment(fullPath)
            && IsTopLevelExtensionChange(root, fullPath);
    }

    private void OnDirectoryWatcherError(object sender, ErrorEventArgs e)
    {
        var error = e.GetException();

        // On an internal-buffer overflow the OS dropped an unknown set of events, so
        // discovery would silently stop reflecting the extensions directory. Log it and
        // run a full reconciliation to catch up on anything the watcher missed. Other
        // errors (for example the directory going away) are logged for diagnosis.
        Logger.LogError($"Directory watcher error for {ExtensionsPath}: {error.Message}");

        if (error is InternalBufferOverflowException && !_disposed)
        {
            StartObservedBackgroundTask(
                async () =>
                {
                    await RefreshInstalledExtensionsAsync().ConfigureAwait(false);
                },
                "reconcile after directory watcher overflow",
                _reload.Token);
        }
    }

    private void HandleDirectoryEntryUpsert(string changedPath)
    {
        var extensionDirectory = GetExtensionDirectoryForPath(ExtensionsPath, changedPath);
        if (extensionDirectory is null)
        {
            return;
        }

        if (HasGalleryInstallMarker(extensionDirectory))
        {
            return;
        }

        var token = _reload.Token;
        StartObservedBackgroundTask(
            () => HandleDirectoryEntryUpsertAsync(extensionDirectory, token),
            $"install JS extension at {extensionDirectory}",
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
        StartObservedBackgroundTask(
            () => HandleDirectoryEntryRemovedAsync(extensionDirectory),
            $"uninstall JS extension at {extensionDirectory}",
            token);
    }

    private async Task HandleDirectoryEntryUpsertAsync(string extensionDirectory, CancellationToken token)
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
            RaiseProviderAdded(wrapper);
        }
    }

    private async Task HandleDirectoryEntryRemovedAsync(string extensionDirectory)
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
            RaiseProviderRemoved(removed);
        }
    }

    private void StartObservedBackgroundTask(Func<Task> operation, string description, CancellationToken cancellationToken)
    {
        _ = ExtensionTaskCoordinator.RunInBackgroundAsync(
            operation,
            description,
            static (description, ex) => Logger.LogError($"Failed to {description}", ex),
            cancellationToken);
    }

    private Task StopExtensionsConcurrentlyAsync(IReadOnlyList<JSExtensionWrapper> extensions, string operation)
    {
        return ExtensionTaskCoordinator.RunBlockingConcurrentlyAsync(
            extensions,
            extension =>
            {
                extension.ProcessExited -= OnExtensionProcessExited;
                extension.SignalDispose();
            },
            ExtensionTeardownTimeout,
            (extension, ex) => Logger.LogError(
                $"Failed to {operation} JS extension {extension.ExtensionDisplayName}",
                ex),
            () => Logger.LogWarning(
                $"Timed out waiting for {extensions.Count} JS extension(s) to {operation} after {ExtensionTeardownTimeout.TotalSeconds} seconds."));
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

    private async Task<CommandProviderWrapper?> RemoveExtensionByDirectoryGatedAsync(string directory, CancellationToken cancellationToken = default)
    {
        // Cancel and await this directory's crash recovery before taking the gate. Order
        // matters both ways: canceling first releases recovery that is waiting on (or
        // holding) the gate, and awaiting before the gate is taken means the removal is
        // never waiting on the gate while holding it. Draining also closes the directory to
        // new recovery for the duration, so an uninstall cannot race a restart.
        await _recovery.CancelAndDrainAsync(directory).ConfigureAwait(false);

        IDisposable? gate = null;
        try
        {
            gate = await _directoryGate.AcquireAsync(directory, cancellationToken).ConfigureAwait(false);
        }
        catch (ObjectDisposedException)
        {
            // The gate is being torn down; fall through and remove best-effort.
        }
        catch (OperationCanceledException)
        {
            // The uninstall never acquired the lifecycle gate, so this directory is staying put.
            // Reopen crash recovery before handing cancellation back to the caller.
            _recovery.CompleteDirectoryRemoval(directory);
            throw;
        }

        try
        {
            return await RemoveExtensionByDirectoryCoreAsync(directory).ConfigureAwait(false);
        }
        finally
        {
            gate?.Dispose();

            // Release the gate entry for the directory now that it is fully removed.
            _directoryGate.Remove(directory);
            _recovery.CompleteDirectoryRemoval(directory);
        }
    }

    private async Task<CommandProviderWrapper?> RemoveExtensionByDirectoryCoreAsync(string directory)
    {
        JSExtensionWrapper? extensionToRemove;
        CommandProviderWrapper? wrapperToRemove;

        lock (_extensionsLock)
        {
            extensionToRemove = _extensions.FirstOrDefault(e => PathsEqual(e.ManifestDirectory, directory));
            if (extensionToRemove is null)
            {
                wrapperToRemove = null;
            }
            else
            {
                _extensions.Remove(extensionToRemove);
                wrapperToRemove = _providerWrappers.FirstOrDefault(w => ReferenceEquals(w.Extension, extensionToRemove));
                if (wrapperToRemove is not null)
                {
                    _providerWrappers.Remove(wrapperToRemove);
                }

                _crashCounts.Remove(CanonicalKey(directory));
                _providerIds.Release(extensionToRemove.NameKey, CanonicalKey(directory));
            }
        }

        // Always tear down the source watcher for the directory, even when no live
        // extension matched (for example a crash-disabled extension that was already
        // removed from the list but whose watcher was intentionally kept alive), so an
        // uninstall never leaks a watcher.
        StopSourceFileWatcher(directory);

        if (extensionToRemove is not null)
        {
            extensionToRemove.ProcessExited -= OnExtensionProcessExited;
            await extensionToRemove.SignalDisposeAsync().ConfigureAwait(false);
        }

        return wrapperToRemove;
    }

    /// <summary>
    /// Immutable pairing of a live <see cref="FileSystemWatcher"/> and the watch root
    /// (per <see cref="ResolveWatchRoot"/>) it is currently rooted at. Tracking the watch
    /// root alongside the watcher instance is what lets <see cref="EnsureSourceFileWatcher"/>
    /// tell an already-correct watcher (no-op) apart from a stale one whose manifest-declared
    /// root has since moved (repair path), without re-deriving and diffing paths from the
    /// manifest on every ensure call.
    /// </summary>
    private sealed record ExtensionSourceWatcher(FileSystemWatcher Watcher, string WatchRoot);

    /// <summary>
    /// Ensures exactly one source-file watcher is registered for extension directory
    /// <paramref name="directory"/>, rooted at whatever <see cref="ResolveWatchRoot"/>
    /// resolves for <paramref name="manifest"/>. Both initial registration
    /// (<see cref="StartAndRegisterAsync"/>) and hot-reload
    /// (<see cref="HotReloadExtensionAsync"/>) call this to guarantee a watcher exists for the
    /// directory they just (re)loaded, so this must be, and is, idempotent:
    /// <list type="bullet">
    ///   <item><b>No watcher yet:</b> create one at the resolved watch root.</item>
    ///   <item><b>A watcher exists and its watch root still matches:</b> no-op. This is the
    ///   common case; a hot-reload triggered by an unrelated source edit calls this with the
    ///   same manifest that produced the already-live watcher.</item>
    ///   <item><b>A watcher exists but its watch root no longer matches (repair path,
    ///   <see cref="SourceWatcherNeedsRepair"/>):</b> the manifest's <c>cmdpal.watchPath</c>
    ///   changed since the watcher was created, or there is no explicit watchPath and the
    ///   entry point moved to a different directory. The stale watcher is stopped and
    ///   replaced with one rooted at the new watch root, so a live edit under the new root is
    ///   not silently missed until the extension is reinstalled or the host restarts.</item>
    /// </list>
    /// Every branch leaves exactly one watcher registered for <paramref name="directory"/>,
    /// preserving the one-watcher-per-extension-directory invariant across every directory
    /// this service owns.
    /// </summary>
    /// <param name="directory">The extension directory that owns this watcher (the dictionary key).</param>
    /// <param name="manifest">The extension's current manifest, used to resolve the watch root.</param>
    private void EnsureSourceFileWatcher(string directory, JSExtensionManifest manifest)
    {
        // The watch root is manifest-driven (see ResolveWatchRoot): an explicit
        // cmdpal.watchPath wins, otherwise the entry point's own directory is used instead
        // of the whole extension directory, so the host does not have to guess which
        // unrelated subfolders (VCS metadata, docs, and so on) to stay out of.
        var watchRoot = ResolveWatchRoot(directory, manifest);

        lock (_extensionSourceWatchersLock)
        {
            if (_extensionSourceWatchers.TryGetValue(directory, out var existing))
            {
                if (!SourceWatcherNeedsRepair(existing.WatchRoot, watchRoot))
                {
                    // Idempotent no-op: the live watcher already covers the manifest's
                    // current watch root.
                    return;
                }

                // Repair path: the watch root moved while the watcher was live (watchPath
                // edited, or the entry point's directory changed with no explicit
                // watchPath). Tear down the stale watcher before creating its replacement so
                // the directory is never left registered against two watchers at once.
                Logger.LogInfo(
                    $"Source file watcher root changed for {directory} ({existing.WatchRoot} -> {watchRoot}); recreating.");
                DetachAndDisposeSourceFileWatcher(existing.Watcher);
                _extensionSourceWatchers.Remove(directory);
            }

            CreateAndRegisterSourceFileWatcher(directory, watchRoot);
        }
    }

    // Caller must hold _extensionSourceWatchersLock.
    private void CreateAndRegisterSourceFileWatcher(string directory, string watchRoot)
    {
        try
        {
            // Watch all files and filter to the source extensions in the handler so
            // that .js, .mjs, and .cjs edits all trigger a hot-reload.
            var watcher = new FileSystemWatcher(watchRoot)
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName,
                IncludeSubdirectories = true,

                // A source extension recursively watches its own node_modules, so a
                // dependency install can flood the default 8 KB buffer. Enlarge it and
                // recover from any overflow in the Error handler.
                InternalBufferSize = 64 * 1024,
            };

            watcher.Changed += OnSourceFileChanged;
            watcher.Created += OnSourceFileChanged;

            // Editors commonly save atomically (write a temp file, then rename it over
            // the target) and also delete/recreate files. Subscribe to Renamed and
            // Deleted as well so those changes reload instead of being missed.
            watcher.Renamed += OnSourceFileRenamed;
            watcher.Deleted += OnSourceFileChanged;
            watcher.Error += OnSourceWatcherError;

            // Enable events only after every handler is attached so an edit that lands
            // during setup is not dropped.
            watcher.EnableRaisingEvents = true;

            _extensionSourceWatchers[directory] = new ExtensionSourceWatcher(watcher, watchRoot);
            Logger.LogDebug($"Started source file watcher at {watchRoot} for extension {directory}");
        }
        catch (Exception ex)
        {
            Logger.LogError($"Failed to start source file watcher at {watchRoot} for extension {directory}: {ex.Message}");
        }
    }

    // Detaches every handler this service ever attaches to a source watcher and disposes
    // it. Shared by the stop paths and by EnsureSourceFileWatcher's repair path so a
    // watcher is never disposed with a handler still attached (which would leak it).
    private void DetachAndDisposeSourceFileWatcher(FileSystemWatcher watcher)
    {
        watcher.Changed -= OnSourceFileChanged;
        watcher.Created -= OnSourceFileChanged;
        watcher.Renamed -= OnSourceFileRenamed;
        watcher.Deleted -= OnSourceFileChanged;
        watcher.Error -= OnSourceWatcherError;
        watcher.Dispose();
    }

    private void StopSourceFileWatcher(string directory)
    {
        lock (_extensionSourceWatchersLock)
        {
            if (_extensionSourceWatchers.TryGetValue(directory, out var existing))
            {
                DetachAndDisposeSourceFileWatcher(existing.Watcher);
                _extensionSourceWatchers.Remove(directory);
            }
        }

        _hotReloadDebouncer.Cancel(directory);
    }

    private void StopAllSourceFileWatchers()
    {
        lock (_extensionSourceWatchersLock)
        {
            foreach (var existing in _extensionSourceWatchers.Values)
            {
                DetachAndDisposeSourceFileWatcher(existing.Watcher);
            }

            _extensionSourceWatchers.Clear();
        }

        // Advance the debounce generation so a pending hot-reload callback that was already
        // queued before this stop is dropped instead of firing against the next load cycle.
        _hotReloadDebouncer.CancelAll();
    }

    private void OnSourceFileChanged(object sender, FileSystemEventArgs e)
    {
        RouteSourceChange(e.FullPath);
    }

    private void OnSourceFileRenamed(object sender, RenamedEventArgs e)
    {
        // An atomic save writes a temp file and renames it over the target, so the new
        // path is the real source file. Route both the new and old paths so a rename into
        // or out of a watched source name reloads.
        RouteSourceChange(e.FullPath);
        RouteSourceChange(e.OldFullPath);
    }

    private void OnSourceWatcherError(object sender, ErrorEventArgs e)
    {
        var error = e.GetException();

        // Find the directory this watcher belongs to so the recovery targets the right
        // extension. The watcher instance is nested in the dictionary value, so match on
        // reference.
        string? directory = null;
        lock (_extensionSourceWatchersLock)
        {
            foreach (var pair in _extensionSourceWatchers)
            {
                if (ReferenceEquals(pair.Value.Watcher, sender))
                {
                    directory = pair.Key;
                    break;
                }
            }
        }

        Logger.LogError($"Source file watcher error for {directory ?? "(unknown)"}: {error.Message}");

        // A buffer overflow dropped an unknown set of edits, so the extension may be stale.
        // Queue a hot reload of the affected directory to pick up the current on-disk state.
        if (error is InternalBufferOverflowException && directory is not null && !_disposed)
        {
            _hotReloadDebouncer.Notify(directory, directory);
        }
    }

    private void RouteSourceChange(string fullPath)
    {
        if (!ShouldReloadForSourceChange(fullPath))
        {
            return;
        }

        var directory = FindWatchedDirectory(fullPath);
        if (directory is not null)
        {
            _hotReloadDebouncer.Notify(directory, fullPath);
        }
    }

    private string? FindWatchedDirectory(string changedPath)
    {
        lock (_extensionSourceWatchersLock)
        {
            foreach (var directory in _extensionSourceWatchers.Keys)
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
            // Validate then swap. Start the replacement before removing the
            // incumbent, so a failed reload keeps the incumbent provider (and its source
            // watcher) live and a later corrective edit re-triggers this reload. The old
            // provider is only removed once the new one has started and registered, so a
            // duplicate ID refresh never leaves the directory with neither provider.
            var replacement = await StartInstanceAsync(directory, parseResult.Manifest, _reload.Token).ConfigureAwait(false);
            if (replacement is null)
            {
                Logger.LogError(
                    $"Hot-reload failed: {parseResult.Manifest.EffectiveDisplayName} did not restart; keeping the current instance.");
                return;
            }

            var newExtension = replacement.Extension;
            var newWrapper = replacement.Wrapper;
            newExtension.ProcessExited += OnExtensionProcessExited;

            JSExtensionWrapper? incumbentExtension = null;
            CommandProviderWrapper? removedWrapper = null;
            var swapped = false;
            var key = CanonicalKey(directory);

            lock (_extensionsLock)
            {
                if (!_shuttingDown)
                {
                    // Remove the incumbent from the collections and release its provider id
                    // so the replacement can claim it.
                    incumbentExtension = _extensions.FirstOrDefault(e => PathsEqual(e.ManifestDirectory, directory));
                    if (incumbentExtension is not null)
                    {
                        _extensions.Remove(incumbentExtension);
                        removedWrapper = _providerWrappers.FirstOrDefault(w => ReferenceEquals(w.Extension, incumbentExtension));
                        if (removedWrapper is not null)
                        {
                            _providerWrappers.Remove(removedWrapper);
                        }

                        _providerIds.Release(incumbentExtension.NameKey, key);
                    }

                    if (_providerIds.TryReserve(newExtension.NameKey, key))
                    {
                        _extensions.Add(newExtension);
                        _providerWrappers.Add(newWrapper);
                        _crashCounts.Remove(key);
                        swapped = true;
                    }
                    else if (incumbentExtension is not null)
                    {
                        // The replacement's provider id is owned by a different directory.
                        // Restore the incumbent so the reload does not lose both providers.
                        _providerIds.TryReserve(incumbentExtension.NameKey, key);
                        _extensions.Add(incumbentExtension);
                        if (removedWrapper is not null)
                        {
                            _providerWrappers.Add(removedWrapper);
                        }
                    }
                }
            }

            if (!swapped)
            {
                // The swap did not happen (shutdown, or the new provider id collided).
                // Tear down the freshly started replacement and keep the incumbent, which
                // is either still registered (restored above) or being torn down by
                // shutdown. Its source watcher was never stopped, so a later edit reloads.
                newExtension.ProcessExited -= OnExtensionProcessExited;
                newExtension.SignalDispose();
                Logger.LogWarning(
                    $"Hot-reload for {parseResult.Manifest.EffectiveDisplayName} could not register the new instance; keeping the previous one.");
                return;
            }

            // The source watcher for this directory was never stopped, so it is preserved
            // across the reload; ensure one exists for the case where the incumbent had
            // none, and repair it in place if the manifest's watch root moved (see
            // EnsureSourceFileWatcher) since the incumbent's watcher was created.
            EnsureSourceFileWatcher(directory, parseResult.Manifest);

            // Dispose the incumbent only after the replacement is registered, so there is
            // never a window with no provider for this directory.
            if (incumbentExtension is not null)
            {
                incumbentExtension.ProcessExited -= OnExtensionProcessExited;
                incumbentExtension.SignalDispose();
            }

            // Emit the removal and addition as a single ordered pair so consumers observe
            // the swap in a consistent order.
            RaiseProviderSwapped(removedWrapper, newWrapper);

            // Catch an immediate post-init exit the same way the initial registration does.
            if (!newExtension.IsRunning())
            {
                OnExtensionProcessExited(newExtension, EventArgs.Empty);
            }

            Logger.LogInfo($"Hot-reload completed for {parseResult.Manifest.EffectiveDisplayName}");
        }
    }
}
