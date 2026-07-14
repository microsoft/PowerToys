// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using ManagedCommon;
using Microsoft.CmdPal.Common.Services;
using Microsoft.CmdPal.UI.ViewModels.Models;
using Microsoft.CommandPalette.Extensions;
using Windows.Foundation;

namespace Microsoft.CmdPal.UI.ViewModels.Services;

/// <summary>
/// Extension service that manages JavaScript/TypeScript extensions running as individual Node.js processes.
/// Each extension gets its own process communicating over JSON-RPC 2.0 via stdio with LSP-style framing.
/// Supports hot-reload (via FileSystemWatcher), crash recovery, and debug attachment.
/// </summary>
public sealed partial class JsonRpcExtensionService : IExtensionService, IDisposable
{
    private static readonly string ExtensionsPath = GetDefaultExtensionsPath();

    private readonly TaskScheduler _taskScheduler;
    private readonly ICommandProviderCache _commandProviderCache;
    private readonly Lock _extensionsLock = new();
    private readonly List<JSExtensionWrapper> _extensions = [];
    private readonly List<CommandProviderWrapper> _providerWrappers = [];
    private readonly HashSet<string> _disabledExtensions = [];

    private readonly Lock _sourceWatcherLock = new();
    private readonly Dictionary<string, FileSystemWatcher> _sourceFileWatchers = [];
    private readonly Dictionary<string, Timer> _debounceTimers = [];

    private FileSystemWatcher? _directoryWatcher;
    private bool _disposed;

#pragma warning disable CS0067 // Events are required by the interface but not raised by this implementation yet
    public event TypedEventHandler<IExtensionService, IEnumerable<CommandProviderWrapper>>? OnProviderAdded;

    public event TypedEventHandler<IExtensionService, IEnumerable<CommandProviderWrapper>>? OnProviderRemoved;
#pragma warning restore CS0067

    public JsonRpcExtensionService(TaskScheduler taskScheduler, ICommandProviderCache commandProviderCache)
    {
        _taskScheduler = taskScheduler;
        _commandProviderCache = commandProviderCache;
    }

    public async Task<IEnumerable<CommandProviderWrapper>> LoadProvidersAsync(CancellationToken ct)
    {
        if (ct.IsCancellationRequested)
        {
            return [];
        }

        var sw = Stopwatch.StartNew();

        if (!Directory.Exists(ExtensionsPath))
        {
            try
            {
                Directory.CreateDirectory(ExtensionsPath);
                Logger.LogDebug($"Created JS extensions directory: {ExtensionsPath}");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to create JS extensions directory {ExtensionsPath}: {ex.Message}");
                return [];
            }
        }

        var wrappers = await DiscoverAndLoadExtensionsAsync(ExtensionsPath, ct).ConfigureAwait(false);
        StartDirectoryWatcher();

        sw.Stop();
        Logger.LogInfo($"JsonRpcExtensionService: Loaded {wrappers.Count} extension(s) in {sw.ElapsedMilliseconds} ms");

        return wrappers;
    }

    public Task SignalStopAsync()
    {
        StopDirectoryWatcher();
        StopAllSourceFileWatchers();

        lock (_extensionsLock)
        {
            foreach (var ext in _extensions)
            {
                try
                {
                    ext.SignalDispose();
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Failed to stop JS extension {ext.ExtensionDisplayName}: {ex.Message}");
                }
            }

            _extensions.Clear();
            _providerWrappers.Clear();
        }

        return Task.CompletedTask;
    }

    public Task<IEnumerable<IExtensionWrapper>> GetInstalledExtensionsAsync(bool includeDisabledExtensions = false)
    {
        lock (_extensionsLock)
        {
            if (includeDisabledExtensions)
            {
                return Task.FromResult<IEnumerable<IExtensionWrapper>>(_extensions.ToList());
            }

            return Task.FromResult<IEnumerable<IExtensionWrapper>>(
                _extensions.Where(e => !_disabledExtensions.Contains(e.ExtensionUniqueId)).ToList());
        }
    }

    public async Task<IEnumerable<IExtensionWrapper>> RefreshInstalledExtensionsAsync(bool includeDisabledExtensions = false)
    {
        // Re-scan the directory for any new extensions
        if (Directory.Exists(ExtensionsPath))
        {
            var subdirs = Directory.GetDirectories(ExtensionsPath);
            foreach (var subdir in subdirs)
            {
                var manifestPath = Path.Combine(subdir, "package.json");
                if (!File.Exists(manifestPath))
                {
                    continue;
                }

                // Skip if already loaded
                var dirName = Path.GetFileName(subdir);
                bool alreadyLoaded;
                lock (_extensionsLock)
                {
                    alreadyLoaded = _extensions.Any(e => e.ManifestDirectory == subdir);
                }

                if (!alreadyLoaded)
                {
                    await LoadExtensionFromDirectoryAsync(subdir, CancellationToken.None).ConfigureAwait(false);
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
        _disabledExtensions.Remove(extensionUniqueId);
    }

    public void DisableExtension(string extensionUniqueId)
    {
        _disabledExtensions.Add(extensionUniqueId);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        StopDirectoryWatcher();
        StopAllSourceFileWatchers();

        lock (_extensionsLock)
        {
            foreach (var ext in _extensions)
            {
                ext.Dispose();
            }

            _extensions.Clear();
            _providerWrappers.Clear();
        }
    }

    private async Task<List<CommandProviderWrapper>> DiscoverAndLoadExtensionsAsync(string extensionsPath, CancellationToken ct)
    {
        var wrappers = new List<CommandProviderWrapper>();

        if (!Directory.Exists(extensionsPath))
        {
            return wrappers;
        }

        var subdirectories = Directory.GetDirectories(extensionsPath);
        foreach (var subdir in subdirectories)
        {
            if (ct.IsCancellationRequested)
            {
                break;
            }

            var wrapper = await LoadExtensionFromDirectoryAsync(subdir, ct).ConfigureAwait(false);
            if (wrapper != null)
            {
                wrappers.Add(wrapper);
            }
        }

        return wrappers;
    }

    private async Task<CommandProviderWrapper?> LoadExtensionFromDirectoryAsync(string extensionDirectory, CancellationToken ct)
    {
        var manifestPath = Path.Combine(extensionDirectory, "package.json");
        if (!File.Exists(manifestPath))
        {
            return null;
        }

        try
        {
            var manifest = await JSExtensionManifest.LoadFromFileAsync(manifestPath).ConfigureAwait(false);
            if (manifest == null)
            {
                Logger.LogWarning($"Invalid manifest at {manifestPath}");
                return null;
            }

            var extensionWrapper = new JSExtensionWrapper(manifest, extensionDirectory);

            lock (_extensionsLock)
            {
                _extensions.Add(extensionWrapper);
            }

            await extensionWrapper.StartExtensionAsync().ConfigureAwait(false);

            if (!extensionWrapper.IsRunning())
            {
                Logger.LogError($"Failed to start JS extension {manifest.DisplayName ?? manifest.Name}");
                return null;
            }

            var provider = await extensionWrapper.GetProviderAsync<ICommandProvider>().ConfigureAwait(false);
            if (provider == null)
            {
                Logger.LogWarning($"JS extension {manifest.DisplayName ?? manifest.Name} does not provide ICommandProvider");
                return null;
            }

            var wrapper = new CommandProviderWrapper(extensionWrapper, _taskScheduler, _commandProviderCache);

            lock (_extensionsLock)
            {
                _providerWrappers.Add(wrapper);
            }

            Logger.LogInfo($"Loaded JS extension: {manifest.DisplayName ?? manifest.Name}");

            // Start source file watcher for hot-reload in dev mode
            StartSourceFileWatcher(extensionWrapper, extensionDirectory);

            return wrapper;
        }
        catch (Exception ex)
        {
            Logger.LogError($"Failed to load JS extension from {extensionDirectory}: {ex.Message}");
            return null;
        }
    }

    private void StartDirectoryWatcher()
    {
        if (!Directory.Exists(ExtensionsPath))
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
        if (_directoryWatcher != null)
        {
            _directoryWatcher.Created -= OnExtensionDirectoryCreated;
            _directoryWatcher.Deleted -= OnExtensionDirectoryDeleted;
            _directoryWatcher.Dispose();
            _directoryWatcher = null;
        }
    }

    private void OnExtensionDirectoryCreated(object sender, FileSystemEventArgs e)
    {
        _ = Task.Run(async () =>
        {
            // Small delay to let files finish copying
            await Task.Delay(500).ConfigureAwait(false);
            var wrapper = await LoadExtensionFromDirectoryAsync(e.FullPath, CancellationToken.None).ConfigureAwait(false);
            if (wrapper != null)
            {
                OnProviderAdded?.Invoke(this, [wrapper]);
            }
        });
    }

    private void OnExtensionDirectoryDeleted(object sender, FileSystemEventArgs e)
    {
        _ = Task.Run(async () =>
        {
            await RemoveExtensionByDirectoryAsync(e.FullPath).ConfigureAwait(false);
        });
    }

    private Task RemoveExtensionByDirectoryAsync(string directoryPath)
    {
        JSExtensionWrapper? extensionToRemove = null;
        CommandProviderWrapper? wrapperToRemove = null;

        lock (_extensionsLock)
        {
            extensionToRemove = _extensions.FirstOrDefault(e => e.ManifestDirectory == directoryPath);
            if (extensionToRemove != null)
            {
                _extensions.Remove(extensionToRemove);
                wrapperToRemove = _providerWrappers.FirstOrDefault(w => w.Extension == extensionToRemove);
                if (wrapperToRemove != null)
                {
                    _providerWrappers.Remove(wrapperToRemove);
                }
            }
        }

        if (extensionToRemove != null)
        {
            StopSourceFileWatcher(directoryPath);
            extensionToRemove.SignalDispose();

            if (wrapperToRemove != null)
            {
                OnProviderRemoved?.Invoke(this, [wrapperToRemove]);
            }
        }

        return Task.CompletedTask;
    }

    private void StartSourceFileWatcher(JSExtensionWrapper extensionWrapper, string extensionDirectory)
    {
        try
        {
            var watcher = new FileSystemWatcher(extensionDirectory, "*.js")
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName,
                IncludeSubdirectories = true,
                EnableRaisingEvents = true,
            };

            watcher.Changed += (sender, e) => OnSourceFileChanged(extensionWrapper, extensionDirectory, e);
            watcher.Created += (sender, e) => OnSourceFileChanged(extensionWrapper, extensionDirectory, e);

            lock (_sourceWatcherLock)
            {
                _sourceFileWatchers[extensionDirectory] = watcher;
            }

            Logger.LogDebug($"Started source file watcher for {extensionWrapper.ExtensionDisplayName} at {extensionDirectory}");
        }
        catch (Exception ex)
        {
            Logger.LogError($"Failed to start source file watcher for {extensionWrapper.ExtensionDisplayName}: {ex.Message}");
        }
    }

    private void StopSourceFileWatcher(string extensionDirectory)
    {
        lock (_sourceWatcherLock)
        {
            if (_sourceFileWatchers.TryGetValue(extensionDirectory, out var watcher))
            {
                watcher.EnableRaisingEvents = false;
                watcher.Dispose();
                _sourceFileWatchers.Remove(extensionDirectory);
            }

            if (_debounceTimers.TryGetValue(extensionDirectory, out var timer))
            {
                timer.Dispose();
                _debounceTimers.Remove(extensionDirectory);
            }
        }
    }

    private void StopAllSourceFileWatchers()
    {
        lock (_sourceWatcherLock)
        {
            foreach (var kvp in _sourceFileWatchers)
            {
                kvp.Value.EnableRaisingEvents = false;
                kvp.Value.Dispose();
            }

            _sourceFileWatchers.Clear();

            foreach (var kvp in _debounceTimers)
            {
                kvp.Value.Dispose();
            }

            _debounceTimers.Clear();
        }
    }

    private void OnSourceFileChanged(JSExtensionWrapper extensionWrapper, string extensionDirectory, FileSystemEventArgs e)
    {
        // Skip node_modules changes
        if (e.FullPath.Contains("node_modules", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        lock (_sourceWatcherLock)
        {
            if (_debounceTimers.TryGetValue(extensionDirectory, out var existingTimer))
            {
                existingTimer.Dispose();
            }

            _debounceTimers[extensionDirectory] = new Timer(
                _ => _ = Task.Run(() => RestartExtensionAsync(extensionWrapper)),
                null,
                500,
                Timeout.Infinite);
        }
    }

    private async Task RestartExtensionAsync(JSExtensionWrapper extensionWrapper)
    {
        if (!extensionWrapper.IsHealthy)
        {
            Logger.LogWarning($"Skipping hot-reload for unhealthy extension {extensionWrapper.ExtensionDisplayName}");
            return;
        }

        Logger.LogInfo($"Hot-reload: restarting {extensionWrapper.ExtensionDisplayName}");

        await extensionWrapper.RestartAsync().ConfigureAwait(false);

        if (!extensionWrapper.IsRunning())
        {
            Logger.LogError($"Hot-reload failed: {extensionWrapper.ExtensionDisplayName} did not restart");
            return;
        }

        Logger.LogInfo($"Hot-reload completed for {extensionWrapper.ExtensionDisplayName}");
    }

    private static string GetDefaultExtensionsPath()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(localAppData, "Microsoft", "PowerToys", "CommandPalette", "JSExtensions");
    }
}
