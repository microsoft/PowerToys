// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using Microsoft.CmdPal.UI.ViewModels.Models;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.Extensions.Logging;
using Windows.Foundation;

namespace Microsoft.CmdPal.UI.ViewModels.Services;

public partial class JavaScriptExtensionService : IExtensionService, IDisposable
{
    public event TypedEventHandler<IExtensionService, IEnumerable<CommandProviderWrapper>>? OnCommandProviderAdded;

    public event TypedEventHandler<IExtensionService, IEnumerable<CommandProviderWrapper>>? OnCommandProviderRemoved;

    public event TypedEventHandler<CommandProviderWrapper, IEnumerable<TopLevelViewModel>>? OnCommandsAdded;

    public event TypedEventHandler<CommandProviderWrapper, IEnumerable<TopLevelViewModel>>? OnCommandsRemoved;

    private readonly ILogger _logger;
    private readonly TaskScheduler _taskScheduler;
    private readonly HotkeyManager _hotkeyManager;
    private readonly AliasManager _aliasManager;
    private readonly SettingsService _settingsService;

    private readonly Lock _extensionsLock = new();
    private readonly List<JSExtensionWrapper> _jsExtensions = [];
    private readonly List<CommandProviderWrapper> _jsCommandWrappers = [];
    private readonly SemaphoreSlim _getJSCommandWrappersLock = new(1, 1);

    private readonly List<CommandProviderWrapper> _enabledJSCommandWrappers = [];
    private readonly SemaphoreSlim _getEnabledJSCommandWrappersLock = new(1, 1);

    private readonly List<TopLevelViewModel> _topLevelCommands = [];
    private readonly SemaphoreSlim _getTopLevelCommandsLock = new(1, 1);

    private readonly Lock _sourceWatcherLock = new();
    private readonly Dictionary<string, FileSystemWatcher> _sourceFileWatchers = [];
    private readonly Dictionary<string, Timer> _debounceTimers = [];

    private WeakReference<IPageContext>? _weakPageContext;
    private FileSystemWatcher? _fileSystemWatcher;
    private bool _developmentMode = true;
    private bool _isLoaded;
    private bool _isDisposed;

    public JavaScriptExtensionService(
        TaskScheduler taskScheduler,
        HotkeyManager hotkeyManager,
        AliasManager aliasManager,
        SettingsService settingsService,
        ILogger logger)
    {
        _logger = logger;
        _taskScheduler = taskScheduler;
        _hotkeyManager = hotkeyManager;
        _aliasManager = aliasManager;
        _settingsService = settingsService;
    }

    public async Task SignalStartExtensionsAsync(WeakReference<IPageContext> weakPageContext)
    {
        _weakPageContext = weakPageContext;

        if (!_isLoaded)
        {
            await LoadJSExtensionsAsync();
            StartFileWatcher();
        }
    }

    public async Task SignalStopExtensionsAsync()
    {
        StopFileWatcher();
        StopAllSourceFileWatchers();

        await _getJSCommandWrappersLock.WaitAsync();
        try
        {
            foreach (var wrapper in _jsCommandWrappers)
            {
                try
                {
                    wrapper.Extension?.SignalDispose();
                }
                catch (Exception ex)
                {
                    Log_FailedToStopExtension(wrapper.DisplayName, ex);
                }
            }

            _jsCommandWrappers.Clear();
        }
        finally
        {
            _getJSCommandWrappersLock.Release();
        }

        lock (_extensionsLock)
        {
            _jsExtensions.Clear();
        }
    }

    public async Task EnableProviderAsync(string providerId)
    {
        await _getEnabledJSCommandWrappersLock.WaitAsync();
        try
        {
            foreach (var wrapper in _enabledJSCommandWrappers)
            {
                if (wrapper.Id.Equals(providerId, StringComparison.Ordinal))
                {
                    return;
                }
            }
        }
        finally
        {
            _getEnabledJSCommandWrappersLock.Release();
        }

        await _getJSCommandWrappersLock.WaitAsync();
        try
        {
            CommandProviderWrapper? wrapper = null;

            foreach (var jsWrapper in _jsCommandWrappers)
            {
                if (jsWrapper.Id.Equals(providerId, StringComparison.Ordinal))
                {
                    wrapper = jsWrapper;
                }
            }

            if (wrapper != null)
            {
                await _getEnabledJSCommandWrappersLock.WaitAsync();
                try
                {
                    _enabledJSCommandWrappers.Add(wrapper);
                }
                finally
                {
                    _getEnabledJSCommandWrappersLock.Release();
                }

                var commands = await LoadTopLevelCommandsFromProvider(wrapper);
                await _getTopLevelCommandsLock.WaitAsync();
                try
                {
                    foreach (var c in commands)
                    {
                        _topLevelCommands.Add(c);
                    }
                }
                finally
                {
                    _getTopLevelCommandsLock.Release();
                }

                OnCommandsAdded?.Invoke(wrapper, commands);
            }
        }
        finally
        {
            _getJSCommandWrappersLock.Release();
        }
    }

    public async Task DisableProviderAsync(string providerId)
    {
        await _getEnabledJSCommandWrappersLock.WaitAsync();
        try
        {
            CommandProviderWrapper? wrapper = null;

            foreach (var enabledWrapper in _enabledJSCommandWrappers)
            {
                if (enabledWrapper.Id.Equals(providerId, StringComparison.Ordinal))
                {
                    wrapper = enabledWrapper;
                }
            }

            if (wrapper != null)
            {
                _enabledJSCommandWrappers.Remove(wrapper);

                await _getTopLevelCommandsLock.WaitAsync();
                try
                {
                    List<TopLevelViewModel> commands = [];

                    foreach (var topLevelCommand in _topLevelCommands)
                    {
                        if (topLevelCommand.CommandProviderId.Equals(wrapper.Id, StringComparison.Ordinal))
                        {
                            commands.Add(topLevelCommand);
                        }
                    }

                    foreach (var c in commands)
                    {
                        _topLevelCommands.Remove(c);
                    }

                    OnCommandsRemoved?.Invoke(wrapper, commands);
                }
                finally
                {
                    _getTopLevelCommandsLock.Release();
                }
            }
        }
        finally
        {
            _getEnabledJSCommandWrappersLock.Release();
        }
    }

    private async Task LoadJSExtensionsAsync()
    {
        var s = new Stopwatch();
        s.Start();

        var extensionsPath = GetDefaultExtensionsPath();
        if (!Directory.Exists(extensionsPath))
        {
            try
            {
                Directory.CreateDirectory(extensionsPath);
                Log_CreatedExtensionsDirectory(extensionsPath);
            }
            catch (Exception ex)
            {
                Log_FailedToCreateExtensionsDirectory(extensionsPath, ex);
                _isLoaded = true;
                return;
            }
        }

        await DiscoverAndLoadExtensionsFromPathAsync(extensionsPath);

        s.Stop();
        _isLoaded = true;

        Log_LoadingJSExtensionsTook(s.ElapsedMilliseconds);
    }

    private async Task DiscoverAndLoadExtensionsFromPathAsync(string extensionsPath)
    {
        if (!Directory.Exists(extensionsPath))
        {
            return;
        }

        var subdirectories = Directory.GetDirectories(extensionsPath);

        foreach (var subdir in subdirectories)
        {
            await LoadExtensionFromDirectoryAsync(subdir);
        }
    }

    private async Task LoadExtensionFromDirectoryAsync(string extensionDirectory)
    {
        var manifestPath = Path.Combine(extensionDirectory, "cmdpal.json");
        if (!File.Exists(manifestPath))
        {
            return;
        }

        try
        {
            var manifest = await JSExtensionManifest.LoadFromFileAsync(manifestPath);
            if (manifest == null)
            {
                Log_InvalidManifest(manifestPath);
                return;
            }

            var extensionWrapper = new JSExtensionWrapper(manifest, extensionDirectory);

            lock (_extensionsLock)
            {
                _jsExtensions.Add(extensionWrapper);
            }

            await extensionWrapper.StartExtensionAsync();

            if (!extensionWrapper.IsRunning())
            {
                Log_FailedToStartJSExtension(manifest.DisplayName ?? manifest.Name ?? "Unknown");
                return;
            }

            var provider = await extensionWrapper.GetProviderAsync<ICommandProvider>();
            if (provider == null)
            {
                Log_NoCommandProvider(manifest.DisplayName ?? manifest.Name ?? "Unknown");
                return;
            }

            CommandProviderWrapper wrapper = new(extensionWrapper, provider, _taskScheduler, _hotkeyManager, _aliasManager, _logger);
            lock (_getJSCommandWrappersLock)
            {
                _jsCommandWrappers.Add(wrapper);
            }

            var providerSettings = _settingsService.CurrentSettings.GetProviderSettings(wrapper);

            lock (_getEnabledJSCommandWrappersLock)
            {
                _enabledJSCommandWrappers.Add(wrapper);
            }

            var commands = await LoadTopLevelCommandsFromProvider(wrapper);
            lock (_getTopLevelCommandsLock)
            {
                foreach (var c in commands)
                {
                    _topLevelCommands.Add(c);
                }
            }

            OnCommandProviderAdded?.Invoke(this, new[] { wrapper });
            OnCommandsAdded?.Invoke(wrapper, commands);

            Log_LoadedJSExtension(manifest.DisplayName ?? manifest.Name ?? "Unknown");

            if (_developmentMode)
            {
                StartSourceFileWatcher(extensionWrapper, extensionDirectory);
            }
        }
        catch (Exception ex)
        {
            Log_FailedToLoadExtension(extensionDirectory, ex);
        }
    }

    private async Task<IEnumerable<TopLevelViewModel>> LoadTopLevelCommandsFromProvider(CommandProviderWrapper commandProvider)
    {
        await commandProvider.LoadTopLevelCommands(_settingsService, _weakPageContext!);

        var commands = await Task.Factory.StartNew(
            () =>
            {
                List<TopLevelViewModel> commands = [];
                foreach (var item in commandProvider.TopLevelItems)
                {
                    commands.Add(item);
                }

                foreach (var item in commandProvider.FallbackItems)
                {
                    if (item.IsEnabled)
                    {
                        commands.Add(item);
                    }
                }

                return commands;
            },
            CancellationToken.None,
            TaskCreationOptions.None,
            _taskScheduler);

        commandProvider.CommandsChanged -= CommandProvider_CommandsChanged;
        commandProvider.CommandsChanged += CommandProvider_CommandsChanged;

        return commands;
    }

    private void CommandProvider_CommandsChanged(CommandProviderWrapper sender, IItemsChangedEventArgs args) =>
        _ = Task.Run(async () => await UpdateCommandsForProvider(sender, args));

    private async Task UpdateCommandsForProvider(CommandProviderWrapper sender, IItemsChangedEventArgs args)
    {
        var topLevelItems = await LoadTopLevelCommandsFromProvider(sender);

        List<TopLevelViewModel> newTopLevelItems = [.. topLevelItems];
        foreach (var i in sender.FallbackItems)
        {
            if (i.IsEnabled)
            {
                newTopLevelItems.Add(i);
            }
        }

        await _getTopLevelCommandsLock.WaitAsync();
        try
        {
            List<TopLevelViewModel> clone = [.. _topLevelCommands];
            clone.RemoveAll(item => item.CommandProviderId == sender.ProviderId);
            clone.AddRange(newTopLevelItems);

            ListHelpers.InPlaceUpdateList(_topLevelCommands, clone);
        }
        finally
        {
            _getTopLevelCommandsLock.Release();
        }

        return;
    }

    private void StartFileWatcher()
    {
        var extensionsPath = GetDefaultExtensionsPath();
        if (!Directory.Exists(extensionsPath))
        {
            return;
        }

        try
        {
            _fileSystemWatcher = new FileSystemWatcher(extensionsPath)
            {
                NotifyFilter = NotifyFilters.DirectoryName,
                EnableRaisingEvents = true,
            };

            _fileSystemWatcher.Created += OnExtensionDirectoryCreated;
            _fileSystemWatcher.Deleted += OnExtensionDirectoryDeleted;

            Log_StartedFileWatcher(extensionsPath);
        }
        catch (Exception ex)
        {
            Log_FailedToStartFileWatcher(extensionsPath, ex);
        }
    }

    private void StopFileWatcher()
    {
        if (_fileSystemWatcher != null)
        {
            _fileSystemWatcher.Created -= OnExtensionDirectoryCreated;
            _fileSystemWatcher.Deleted -= OnExtensionDirectoryDeleted;
            _fileSystemWatcher.Dispose();
            _fileSystemWatcher = null;
        }
    }

    private void OnExtensionDirectoryCreated(object sender, FileSystemEventArgs e)
    {
        _ = Task.Run(async () =>
        {
            await Task.Delay(500);
            await LoadExtensionFromDirectoryAsync(e.FullPath);
        });
    }

    private void OnExtensionDirectoryDeleted(object sender, FileSystemEventArgs e)
    {
        _ = Task.Run(async () =>
        {
            await RemoveExtensionByDirectoryAsync(e.FullPath);
        });
    }

    private async Task RemoveExtensionByDirectoryAsync(string directoryPath)
    {
        JSExtensionWrapper? extensionToRemove = null;

        lock (_extensionsLock)
        {
            foreach (var ext in _jsExtensions)
            {
                if (ext.ExtensionUniqueId.Contains(Path.GetFileName(directoryPath)))
                {
                    extensionToRemove = ext;
                    break;
                }
            }

            if (extensionToRemove != null)
            {
                _jsExtensions.Remove(extensionToRemove);
            }
        }

        if (extensionToRemove != null)
        {
            List<CommandProviderWrapper> wrappersToRemove = [];

            await _getJSCommandWrappersLock.WaitAsync();
            try
            {
                foreach (var wrapper in _jsCommandWrappers)
                {
                    if (wrapper.Extension == extensionToRemove)
                    {
                        wrappersToRemove.Add(wrapper);
                    }
                }

                foreach (var wrapper in wrappersToRemove)
                {
                    _jsCommandWrappers.Remove(wrapper);

                    await _getEnabledJSCommandWrappersLock.WaitAsync();
                    try
                    {
                        _enabledJSCommandWrappers.Remove(wrapper);
                    }
                    finally
                    {
                        _getEnabledJSCommandWrappersLock.Release();
                    }

                    await _getTopLevelCommandsLock.WaitAsync();
                    try
                    {
                        List<TopLevelViewModel> commandsToRemove = [];
                        foreach (var cmd in _topLevelCommands)
                        {
                            if (cmd.CommandProviderId.Equals(wrapper.Id, StringComparison.Ordinal))
                            {
                                commandsToRemove.Add(cmd);
                            }
                        }

                        foreach (var cmd in commandsToRemove)
                        {
                            _topLevelCommands.Remove(cmd);
                        }

                        OnCommandsRemoved?.Invoke(wrapper, commandsToRemove);
                    }
                    finally
                    {
                        _getTopLevelCommandsLock.Release();
                    }
                }
            }
            finally
            {
                _getJSCommandWrappersLock.Release();
            }

            OnCommandProviderRemoved?.Invoke(this, wrappersToRemove);

            StopSourceFileWatcher(directoryPath);
            extensionToRemove.SignalDispose();
        }
    }

    private static string GetDefaultExtensionsPath()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(localAppData, "Microsoft", "PowerToys", "CommandPalette", "JSExtensions");
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

            Log_StartedSourceFileWatcher(extensionWrapper.ExtensionDisplayName, extensionDirectory);
        }
        catch (Exception ex)
        {
            Log_FailedToStartSourceFileWatcher(extensionWrapper.ExtensionDisplayName, ex);
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
        lock (_sourceWatcherLock)
        {
            if (_debounceTimers.TryGetValue(extensionDirectory, out var existingTimer))
            {
                existingTimer.Dispose();
            }

            _debounceTimers[extensionDirectory] = new Timer(
                _ => _ = Task.Run(() => RestartExtensionAsync(extensionWrapper, extensionDirectory)),
                null,
                500,
                Timeout.Infinite);
        }
    }

    private async Task RestartExtensionAsync(JSExtensionWrapper extensionWrapper, string extensionDirectory)
    {
        if (!extensionWrapper.IsHealthy)
        {
            Log_SkippingRestartUnhealthy(extensionWrapper.ExtensionDisplayName);
            return;
        }

        Log_HotReloadTriggered(extensionWrapper.ExtensionDisplayName, extensionDirectory);

        // Collect current commands to remove from UI
        CommandProviderWrapper? commandWrapper = null;
        List<TopLevelViewModel> commandsToRemove = [];

        await _getJSCommandWrappersLock.WaitAsync();
        try
        {
            foreach (var wrapper in _jsCommandWrappers)
            {
                if (wrapper.Extension == extensionWrapper)
                {
                    commandWrapper = wrapper;
                    break;
                }
            }
        }
        finally
        {
            _getJSCommandWrappersLock.Release();
        }

        if (commandWrapper != null)
        {
            await _getTopLevelCommandsLock.WaitAsync();
            try
            {
                foreach (var cmd in _topLevelCommands)
                {
                    if (cmd.CommandProviderId.Equals(commandWrapper.Id, StringComparison.Ordinal))
                    {
                        commandsToRemove.Add(cmd);
                    }
                }

                foreach (var cmd in commandsToRemove)
                {
                    _topLevelCommands.Remove(cmd);
                }
            }
            finally
            {
                _getTopLevelCommandsLock.Release();
            }

            OnCommandsRemoved?.Invoke(commandWrapper, commandsToRemove);
        }

        // Restart the Node.js process
        await extensionWrapper.RestartAsync();

        if (!extensionWrapper.IsRunning())
        {
            Log_FailedToRestartJSExtension(extensionWrapper.ExtensionDisplayName);
            return;
        }

        // Re-add commands after restart
        if (commandWrapper != null)
        {
            var commands = await LoadTopLevelCommandsFromProvider(commandWrapper);
            await _getTopLevelCommandsLock.WaitAsync();
            try
            {
                foreach (var c in commands)
                {
                    _topLevelCommands.Add(c);
                }
            }
            finally
            {
                _getTopLevelCommandsLock.Release();
            }

            OnCommandsAdded?.Invoke(commandWrapper, commands);
        }

        Log_HotReloadCompleted(extensionWrapper.ExtensionDisplayName);
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;

        StopFileWatcher();
        StopAllSourceFileWatchers();

        _getJSCommandWrappersLock.Dispose();
        _getEnabledJSCommandWrappersLock.Dispose();
        _getTopLevelCommandsLock.Dispose();

        lock (_extensionsLock)
        {
            foreach (var ext in _jsExtensions)
            {
                ext.SignalDispose();
            }

            _jsExtensions.Clear();
        }

        GC.SuppressFinalize(this);
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Loading JS extensions took {elapsedMs}ms")]
    private partial void Log_LoadingJSExtensionsTook(long elapsedMs);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Created extensions directory at {path}")]
    private partial void Log_CreatedExtensionsDirectory(string path);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to create extensions directory at {path}")]
    private partial void Log_FailedToCreateExtensionsDirectory(string path, Exception ex);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Invalid manifest at {manifestPath}")]
    private partial void Log_InvalidManifest(string manifestPath);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to start JS extension {extensionName}")]
    private partial void Log_FailedToStartJSExtension(string extensionName);

    [LoggerMessage(Level = LogLevel.Warning, Message = "JS extension {extensionName} does not provide ICommandProvider")]
    private partial void Log_NoCommandProvider(string extensionName);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Loaded JS extension {extensionName}")]
    private partial void Log_LoadedJSExtension(string extensionName);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to load extension from {extensionDirectory}")]
    private partial void Log_FailedToLoadExtension(string extensionDirectory, Exception ex);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Started file watcher for {path}")]
    private partial void Log_StartedFileWatcher(string path);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to start file watcher for {path}")]
    private partial void Log_FailedToStartFileWatcher(string path, Exception ex);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to stop extension {extensionName}")]
    private partial void Log_FailedToStopExtension(string extensionName, Exception ex);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Started source file watcher for {extensionName} at {path}")]
    private partial void Log_StartedSourceFileWatcher(string extensionName, string path);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to start source file watcher for {extensionName}")]
    private partial void Log_FailedToStartSourceFileWatcher(string extensionName, Exception ex);

    [LoggerMessage(Level = LogLevel.Information, Message = "Hot-reload triggered for {extensionName} from {path}")]
    private partial void Log_HotReloadTriggered(string extensionName, string path);

    [LoggerMessage(Level = LogLevel.Information, Message = "Hot-reload completed for {extensionName}")]
    private partial void Log_HotReloadCompleted(string extensionName);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to restart JS extension {extensionName} during hot-reload")]
    private partial void Log_FailedToRestartJSExtension(string extensionName);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Skipping hot-reload restart for unhealthy extension {extensionName}")]
    private partial void Log_SkippingRestartUnhealthy(string extensionName);
}
