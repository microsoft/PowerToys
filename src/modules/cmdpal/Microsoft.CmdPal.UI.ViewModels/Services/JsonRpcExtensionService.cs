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
public sealed partial class JsonRpcExtensionService : IExtensionService, IDisposable
{
    // Consecutive crashes above this threshold disable an extension instead of restarting it.
    private const int MaxRestartAttempts = 3;

    // Source-file extensions that trigger a hot-reload, per the manifest contract.
    private static readonly string[] WatchedSourceExtensions = [".js", ".mjs", ".cjs"];

    // Path segments that never carry a relevant manifest or source change. Churn under
    // these (npm writing hundreds of files under node_modules during an install, or git
    // metadata) must not drive discovery or hot-reload, or it causes a restart storm.
    private static readonly string[] IgnoredDirectorySegments = ["node_modules", ".git"];

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

    // Provider-id (normalized manifest name key) reservations shared by every
    // registration path. Consulted and claimed atomically under _extensionsLock so a
    // duplicate id can never register regardless of how it arrives (initial scan,
    // refresh, dynamic install, hot-reload, or crash-restart).
    private readonly ProviderIdReservations _providerIds = new();

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
            _providerIds.Clear();
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
            _providerIds.Clear();
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
    /// Returns true when any directory segment of <paramref name="path"/> is one the
    /// watchers must ignore (for example <c>node_modules</c> or <c>.git</c>). This is a
    /// segment-aware check, so a directory named "node_modules_backup" is not matched.
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

            var outcome = RegistrationOutcome.Added;
            lock (_extensionsLock)
            {
                // The per-directory gate prevents concurrent loads for one directory, but
                // keep the incumbent and drop the newcomer defensively rather than leaking
                // two live processes if a duplicate ever slips through.
                if (_extensions.Any(e => PathsEqual(e.ManifestDirectory, directory)))
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

            StartSourceFileWatcher(directory);

            // A process can exit immediately after init (for example a provider that faults
            // on first use). If that exit fired before we subscribed to ProcessExited above,
            // the event was missed; detect the dead process here and drive the same crash
            // path so an immediate post-init crash is handled (restart or disable) instead
            // of being registered as healthy. The handler runs on a separate task so it
            // acquires the directory gate only after this registration releases it, and it is
            // idempotent, so racing the real event is harmless.
            if (!extensionWrapper.IsRunning())
            {
                OnExtensionProcessExited(extensionWrapper, EventArgs.Empty);
            }

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

                // Free the provider id as part of the same atomic removal so a different
                // extension can claim it, and so the restart below can re-reserve it.
                _providerIds.Release(wrapper.NameKey, key);
            }

            wrapper.ProcessExited -= OnExtensionProcessExited;

            if (removed is not null)
            {
                OnProviderRemoved?.Invoke(this, [removed]);
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
        // Ignore churn under node_modules/.git (for example npm writing many package.json
        // files during an install) so it cannot drive a discovery or hot-reload storm.
        if (HasIgnoredDirectorySegment(e.FullPath))
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
        // A rename can be an atomic promotion (temp -> final) or a demotion/uninstall
        // (final -> temp). Treat the new name as a possible install and the old name as
        // a possible removal, ignoring either side that sits under an ignored segment.
        if (!HasIgnoredDirectorySegment(e.FullPath) && (IsManifestPath(e.FullPath) || Directory.Exists(e.FullPath)))
        {
            HandleDirectoryEntryUpsert(e.FullPath);
        }

        if (!HasIgnoredDirectorySegment(e.OldFullPath))
        {
            HandleDirectoryEntryRemoved(e.OldFullPath);
        }
    }

    private void OnDirectoryWatcherDeleted(object sender, FileSystemEventArgs e)
    {
        if (HasIgnoredDirectorySegment(e.FullPath))
        {
            return;
        }

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
            extensionToRemove.SignalDispose();
        }

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

                // Editors commonly save atomically (write a temp file, then rename it over
                // the target) and also delete/recreate files. Subscribe to Renamed and
                // Deleted as well so those changes reload instead of being missed.
                watcher.Renamed += OnSourceFileRenamed;
                watcher.Deleted += OnSourceFileChanged;

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
                watcher.Renamed -= OnSourceFileRenamed;
                watcher.Deleted -= OnSourceFileChanged;
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
                watcher.Renamed -= OnSourceFileRenamed;
                watcher.Deleted -= OnSourceFileChanged;
                watcher.Dispose();
            }

            _sourceFileWatchers.Clear();
        }
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
