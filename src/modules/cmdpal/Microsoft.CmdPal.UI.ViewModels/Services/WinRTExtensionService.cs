// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CmdPal.Common.Services;
using Microsoft.CmdPal.UI.ViewModels.Models;
using Microsoft.CommandPalette.Extensions;
using Microsoft.Extensions.Logging;
using Windows.ApplicationModel;
using Windows.ApplicationModel.AppExtensions;
using Windows.Foundation;
using Windows.Foundation.Collections;

namespace Microsoft.CmdPal.UI.ViewModels.Services;

/// <summary>
/// WinRT-based extension service that discovers, starts, and manages the lifecycle
/// of Command Palette extensions installed as <c>com.microsoft.commandpalette</c>
/// AppExtensions.  Implements resilient timeout-based loading with background retry
/// for slow extensions.
/// </summary>
public sealed partial class WinRTExtensionService : IExtensionService, IDisposable
{
    // ── Timeout constants (match TopLevelCommandManager) ────────────────
    private static readonly TimeSpan ExtensionStartTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan CommandLoadTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan BackgroundStartTimeout = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan BackgroundCommandLoadTimeout = TimeSpan.FromSeconds(60);

    // ── Events ──────────────────────────────────────────────────────────
    public event TypedEventHandler<IExtensionService, IEnumerable<CommandProviderWrapper>>? OnCommandProviderAdded;

    public event TypedEventHandler<IExtensionService, IEnumerable<CommandProviderWrapper>>? OnCommandProviderRemoved;

    public event TypedEventHandler<CommandProviderWrapper, IEnumerable<TopLevelViewModel>>? OnCommandsAdded;

    public event TypedEventHandler<CommandProviderWrapper, IEnumerable<TopLevelViewModel>>? OnCommandsRemoved;

    // ── Dependencies ────────────────────────────────────────────────────
    private readonly ISettingsService _settingsService;
    private readonly ICommandProviderCache _commandProviderCache;
    private readonly TaskScheduler _mainThread;
    private readonly HotkeyManager _hotkeyManager;
    private readonly AliasManager _aliasManager;
    private readonly ILogger<WinRTExtensionService> _logger;
    private readonly ILoggerFactory _loggerFactory;

    // ── Package discovery ───────────────────────────────────────────────
    private static readonly PackageCatalog _catalog = PackageCatalog.OpenForCurrentUser();
    private const string CreateInstanceProperty = "CreateInstance";
    private const string ClassIdProperty = "@ClassId";

    // ── State ───────────────────────────────────────────────────────────
    private readonly Lock _catalogLock = new();

    private readonly SemaphoreSlim _extensionsLock = new(1, 1);
    private readonly List<IExtensionWrapper> _installedExtensions = [];

    private readonly SemaphoreSlim _allWrappersLock = new(1, 1);
    private readonly List<CommandProviderWrapper> _allWrappers = [];

    private readonly SemaphoreSlim _enabledWrappersLock = new(1, 1);
    private readonly List<CommandProviderWrapper> _enabledWrappers = [];

    // Tracks all loaded items (commands + fallbacks + dock bands) for
    // enable/disable and uninstall book-keeping.
    private readonly SemaphoreSlim _topLevelCommandsLock = new(1, 1);
    private readonly List<TopLevelViewModel> _topLevelCommands = [];

    private WeakReference<IPageContext>? _weakPageContext;
    private CancellationTokenSource _loadCts = new();
    private bool _disposed;

    public WinRTExtensionService(
        ISettingsService settingsService,
        ICommandProviderCache commandProviderCache,
        TaskScheduler mainThread,
        HotkeyManager hotkeyManager,
        AliasManager aliasManager,
        ILoggerFactory loggerFactory)
    {
        _settingsService = settingsService;
        _commandProviderCache = commandProviderCache;
        _mainThread = mainThread;
        _hotkeyManager = hotkeyManager;
        _aliasManager = aliasManager;
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger<WinRTExtensionService>();

        _catalog.PackageInstalling += Catalog_PackageInstalling;
        _catalog.PackageUninstalling += Catalog_PackageUninstalling;
        _catalog.PackageUpdating += Catalog_PackageUpdating;
    }

    // ═══════════════════════════════════════════════════════════════════
    //  IExtensionService
    // ═══════════════════════════════════════════════════════════════════
    public async Task SignalStartExtensionsAsync(WeakReference<IPageContext> weakPageContext)
    {
        _weakPageContext = weakPageContext;
        var ct = _loadCts.Token;
        var timer = Stopwatch.StartNew();

        var extensions = await DiscoverInstalledExtensionsAsync().ConfigureAwait(false);
        await StartExtensionsAndGetCommandsAsync(extensions, ct).ConfigureAwait(false);

        timer.Stop();
        LogLoadingExtensionsCompleted(timer.ElapsedMilliseconds);
    }

    public async Task SignalStopExtensionsAsync()
    {
        // Invalidate all in-flight background loads.
        await _loadCts.CancelAsync().ConfigureAwait(false);
        _loadCts.Dispose();
        _loadCts = new CancellationTokenSource();

        // Dispose running extensions.
        await _extensionsLock.WaitAsync().ConfigureAwait(false);
        try
        {
            foreach (var extension in _installedExtensions)
            {
                LogDisposingExtension(extension.ExtensionUniqueId);
                try
                {
                    if (extension.IsRunning())
                    {
                        extension.SignalDispose();
                    }
                }
                catch (Exception ex)
                {
                    LogFailedSendingDispose(ex, extension.ExtensionUniqueId);
                }
            }

            _installedExtensions.Clear();
        }
        finally
        {
            _extensionsLock.Release();
        }

        // Clear wrapper and command tracking.
        await _allWrappersLock.WaitAsync().ConfigureAwait(false);
        try
        {
            _allWrappers.Clear();
        }
        finally
        {
            _allWrappersLock.Release();
        }

        await _enabledWrappersLock.WaitAsync().ConfigureAwait(false);
        try
        {
            _enabledWrappers.Clear();
        }
        finally
        {
            _enabledWrappersLock.Release();
        }

        await _topLevelCommandsLock.WaitAsync().ConfigureAwait(false);
        try
        {
            _topLevelCommands.Clear();
        }
        finally
        {
            _topLevelCommandsLock.Release();
        }
    }

    public async Task EnableProviderAsync(string providerId)
    {
        // If already enabled, bail.
        await _enabledWrappersLock.WaitAsync().ConfigureAwait(false);
        try
        {
            foreach (var wrapper in _enabledWrappers)
            {
                if (wrapper.ProviderId.Equals(providerId, StringComparison.Ordinal))
                {
                    return;
                }
            }
        }
        finally
        {
            _enabledWrappersLock.Release();
        }

        // Find the wrapper in the full list and enable it.
        await _allWrappersLock.WaitAsync().ConfigureAwait(false);
        try
        {
            CommandProviderWrapper? target = null;

            foreach (var wrapper in _allWrappers)
            {
                if (wrapper.ProviderId.Equals(providerId, StringComparison.Ordinal))
                {
                    target = wrapper;
                    break;
                }
            }

            if (target is not null)
            {
                await _enabledWrappersLock.WaitAsync().ConfigureAwait(false);
                try
                {
                    _enabledWrappers.Add(target);
                }
                finally
                {
                    _enabledWrappersLock.Release();
                }

                var objectSets = await LoadTopLevelCommandsFromProviderAsync(target).ConfigureAwait(false);
                var allItems = CombineTopLevelObjectSets(objectSets);

                await _topLevelCommandsLock.WaitAsync().ConfigureAwait(false);
                try
                {
                    foreach (var item in allItems)
                    {
                        _topLevelCommands.Add(item);
                    }
                }
                finally
                {
                    _topLevelCommandsLock.Release();
                }

                OnCommandsAdded?.Invoke(target, allItems);
            }
        }
        finally
        {
            _allWrappersLock.Release();
        }
    }

    public async Task DisableProviderAsync(string providerId)
    {
        await _enabledWrappersLock.WaitAsync().ConfigureAwait(false);
        try
        {
            CommandProviderWrapper? target = null;

            foreach (var wrapper in _enabledWrappers)
            {
                if (wrapper.ProviderId.Equals(providerId, StringComparison.Ordinal))
                {
                    target = wrapper;
                    break;
                }
            }

            if (target is null)
            {
                return;
            }

            _enabledWrappers.Remove(target);

            await _topLevelCommandsLock.WaitAsync().ConfigureAwait(false);
            try
            {
                List<TopLevelViewModel> removed = [];

                foreach (var cmd in _topLevelCommands)
                {
                    if (cmd.CommandProviderId.Equals(target.ProviderId, StringComparison.Ordinal))
                    {
                        removed.Add(cmd);
                    }
                }

                foreach (var cmd in removed)
                {
                    _topLevelCommands.Remove(cmd);
                }

                OnCommandsRemoved?.Invoke(target, removed);
            }
            finally
            {
                _topLevelCommandsLock.Release();
            }
        }
        finally
        {
            _enabledWrappersLock.Release();
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Extension discovery
    // ═══════════════════════════════════════════════════════════════════
    private async Task<IReadOnlyList<IExtensionWrapper>> DiscoverInstalledExtensionsAsync()
    {
        await _extensionsLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_installedExtensions.Count == 0)
            {
                var appExtensions = await AppExtensionCatalog
                    .Open("com.microsoft.commandpalette")
                    .FindAllAsync();

                foreach (var appExtension in appExtensions)
                {
                    var wrappers = await CreateExtensionWrappersAsync(appExtension).ConfigureAwait(false);
                    _installedExtensions.AddRange(wrappers);
                }
            }

            return [.. _installedExtensions];
        }
        finally
        {
            _extensionsLock.Release();
        }
    }

    private static async Task<List<ExtensionWrapper>> CreateExtensionWrappersAsync(AppExtension extension)
    {
        var (cmdPalProvider, classIds) = await GetCmdPalExtensionPropertiesAsync(extension).ConfigureAwait(false);

        if (cmdPalProvider is null || classIds.Count == 0)
        {
            return [];
        }

        List<ExtensionWrapper> wrappers = [];
        foreach (var classId in classIds)
        {
            wrappers.Add(CreateExtensionWrapper(extension, cmdPalProvider, classId));
        }

        return wrappers;
    }

    private static ExtensionWrapper CreateExtensionWrapper(
        AppExtension extension,
        IPropertySet cmdPalProvider,
        string classId)
    {
        var wrapper = new ExtensionWrapper(extension, classId);

        var supportedInterfaces = GetSubPropertySet(cmdPalProvider, "SupportedInterfaces");
        if (supportedInterfaces is not null)
        {
            foreach (var iface in supportedInterfaces)
            {
                if (Enum.TryParse<ProviderType>(iface.Key, out var pt))
                {
                    wrapper.AddProviderType(pt);
                }
            }
        }

        return wrapper;
    }

    private static async Task<(IPropertySet? CmdPalProvider, List<string> ClassIds)> GetCmdPalExtensionPropertiesAsync(
        AppExtension extension)
    {
        var classIds = new List<string>();
        var properties = await extension.GetExtensionPropertiesAsync();

        if (properties is null)
        {
            return (null, classIds);
        }

        var cmdPalProvider = GetSubPropertySet(properties, "CmdPalProvider");
        if (cmdPalProvider is null)
        {
            return (null, classIds);
        }

        var activation = GetSubPropertySet(cmdPalProvider, "Activation");
        if (activation is null)
        {
            return (cmdPalProvider, classIds);
        }

        classIds.AddRange(GetCreateInstanceList(activation));

        return (cmdPalProvider, classIds);
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Resilient start / load
    // ═══════════════════════════════════════════════════════════════════
    private async Task StartExtensionsAndGetCommandsAsync(
        IEnumerable<IExtensionWrapper> extensions,
        CancellationToken ct)
    {
        var wrapperLogger = _loggerFactory.CreateLogger<CommandProviderWrapper>();

        // Start all extensions in parallel.
        var startResults = await Task.WhenAll(
            extensions.Select(ext => TryStartExtensionAsync(ext, wrapperLogger, ct))).ConfigureAwait(false);

        var startedWrappers = new List<CommandProviderWrapper>();

        foreach (var r in startResults)
        {
            if (r.IsStarted)
            {
                startedWrappers.Add(r.Wrapper);
            }
            else if (r.IsTimedOut)
            {
                _ = StartExtensionWhenReadyAsync(r.Extension, r.PendingStartTask, r.Stopwatch, wrapperLogger, ct);
            }
        }

        await RegisterAndLoadCommandsAsync(startedWrappers, ct).ConfigureAwait(false);
    }

    private async Task<ExtensionStartResult> TryStartExtensionAsync(
        IExtensionWrapper extension,
        ILogger<CommandProviderWrapper> wrapperLogger,
        CancellationToken ct)
    {
        LogStartingExtension(extension.PackageFullName);
        var sw = Stopwatch.StartNew();
        var startTask = extension.StartExtensionAsync();

        try
        {
            await startTask.WaitAsync(ExtensionStartTimeout, ct).ConfigureAwait(false);

            var wrapper = new CommandProviderWrapper(
                extension, _mainThread, _hotkeyManager, _aliasManager, wrapperLogger, _commandProviderCache);

            LogExtensionStarted(extension.PackageFullName, sw.ElapsedMilliseconds);
            return ExtensionStartResult.Started(extension, wrapper);
        }
        catch (TimeoutException)
        {
            LogExtensionStartTimedOut(extension.PackageFullName, sw.ElapsedMilliseconds);
            return ExtensionStartResult.TimedOut(extension, startTask, sw);
        }
        catch (OperationCanceledException)
        {
            LogExtensionStartCancelled(extension.PackageFullName, sw.ElapsedMilliseconds);
            return ExtensionStartResult.Failed(extension);
        }
        catch (Exception ex)
        {
            LogExtensionStartFailed(ex, extension.PackageFullName, sw.ElapsedMilliseconds);
            return ExtensionStartResult.Failed(extension);
        }
    }

    private async Task StartExtensionWhenReadyAsync(
        IExtensionWrapper extension,
        Task startTask,
        Stopwatch sw,
        ILogger<CommandProviderWrapper> wrapperLogger,
        CancellationToken ct)
    {
        try
        {
            await startTask.WaitAsync(BackgroundStartTimeout, ct).ConfigureAwait(false);

            var wrapper = new CommandProviderWrapper(
                extension, _mainThread, _hotkeyManager, _aliasManager, wrapperLogger, _commandProviderCache);

            LogExtensionLateStarted(extension.PackageFullName, sw.ElapsedMilliseconds);
            await RegisterAndLoadCommandsAsync([wrapper], ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Reload or stop happened — discard stale results.
        }
        catch (Exception ex)
        {
            LogBackgroundStartFailed(ex, extension.PackageFullName, sw.ElapsedMilliseconds);
        }
    }

    private async Task RegisterAndLoadCommandsAsync(
        ICollection<CommandProviderWrapper> wrappers,
        CancellationToken ct)
    {
        // Register wrappers in both the "all" and "enabled" lists.
        await _allWrappersLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            _allWrappers.AddRange(wrappers);
        }
        finally
        {
            _allWrappersLock.Release();
        }

        await _enabledWrappersLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            _enabledWrappers.AddRange(wrappers);
        }
        finally
        {
            _enabledWrappersLock.Release();
        }

        if (wrappers.Count > 0)
        {
            OnCommandProviderAdded?.Invoke(this, wrappers);
        }

        // Load commands from each wrapper in parallel.
        var loadResults = await Task.WhenAll(
            wrappers.Select(w => TryLoadCommandsAsync(w, ct))).ConfigureAwait(false);

        foreach (var r in loadResults)
        {
            if (r.IsLoaded)
            {
                var allItems = CombineTopLevelObjectSets(r.TopLevelObjectSets);

                await _topLevelCommandsLock.WaitAsync(ct).ConfigureAwait(false);
                try
                {
                    foreach (var item in allItems)
                    {
                        _topLevelCommands.Add(item);
                    }
                }
                finally
                {
                    _topLevelCommandsLock.Release();
                }

                if (allItems.Count > 0)
                {
                    OnCommandsAdded?.Invoke(r.Wrapper, allItems);
                }
            }
            else if (r.IsTimedOut)
            {
                _ = AppendCommandsWhenReadyAsync(r.Wrapper, r.PendingLoadTask, r.Stopwatch, ct);
            }
        }
    }

    private async Task<CommandLoadResult> TryLoadCommandsAsync(
        CommandProviderWrapper wrapper,
        CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        var loadTask = LoadTopLevelCommandsFromProviderAsync(wrapper);

        try
        {
            var objectSets = await loadTask.WaitAsync(CommandLoadTimeout, ct).ConfigureAwait(false);
            var cmdCount = objectSets.Commands?.Count ?? 0;
            var bandCount = objectSets.DockBands?.Count ?? 0;
            LogCommandsLoaded(wrapper.ExtensionHost?.Extension?.PackageFullName, cmdCount, bandCount, sw.ElapsedMilliseconds);
            return CommandLoadResult.Loaded(wrapper, objectSets);
        }
        catch (TimeoutException)
        {
            LogCommandLoadTimedOut(wrapper.ExtensionHost?.Extension?.PackageFullName, sw.ElapsedMilliseconds);
            return CommandLoadResult.TimedOut(wrapper, loadTask, sw);
        }
        catch (OperationCanceledException)
        {
            return CommandLoadResult.Failed(wrapper);
        }
        catch (Exception ex)
        {
            LogCommandLoadFailed(ex, wrapper.ExtensionHost?.Extension?.PackageFullName, sw.ElapsedMilliseconds);
            return CommandLoadResult.Failed(wrapper);
        }
    }

    private async Task AppendCommandsWhenReadyAsync(
        CommandProviderWrapper wrapper,
        Task<TopLevelObjectSets> loadTask,
        Stopwatch sw,
        CancellationToken ct)
    {
        try
        {
            var objectSets = await loadTask.WaitAsync(BackgroundCommandLoadTimeout, ct).ConfigureAwait(false);
            var allItems = CombineTopLevelObjectSets(objectSets);

            await _topLevelCommandsLock.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                foreach (var item in allItems)
                {
                    _topLevelCommands.Add(item);
                }
            }
            finally
            {
                _topLevelCommandsLock.Release();
            }

            if (allItems.Count > 0)
            {
                OnCommandsAdded?.Invoke(wrapper, allItems);
            }

            var cmdCount = objectSets.Commands?.Count ?? 0;
            var bandCount = objectSets.DockBands?.Count ?? 0;
            LogCommandsLateLoaded(wrapper.ExtensionHost?.Extension?.PackageFullName, cmdCount, bandCount, sw.ElapsedMilliseconds);
        }
        catch (OperationCanceledException)
        {
            // Reload or stop happened — discard stale results.
        }
        catch (Exception ex)
        {
            LogBackgroundCommandLoadFailed(ex, wrapper.ExtensionHost?.Extension?.PackageFullName, sw.ElapsedMilliseconds);
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Command loading helpers
    // ═══════════════════════════════════════════════════════════════════
    private async Task<TopLevelObjectSets> LoadTopLevelCommandsFromProviderAsync(CommandProviderWrapper wrapper)
    {
        await wrapper.LoadTopLevelCommands(_settingsService, _weakPageContext!).ConfigureAwait(false);

        var result = await Task.Factory.StartNew(
            () =>
            {
                List<TopLevelViewModel> commands = [];
                List<TopLevelViewModel> bands = [];

                foreach (var item in wrapper.TopLevelItems)
                {
                    commands.Add(item);
                }

                foreach (var item in wrapper.FallbackItems)
                {
                    if (item.IsEnabled)
                    {
                        commands.Add(item);
                    }
                }

                foreach (var item in wrapper.DockBandItems)
                {
                    bands.Add(item);
                }

                return new TopLevelObjectSets(commands, bands);
            },
            CancellationToken.None,
            TaskCreationOptions.None,
            _mainThread).ConfigureAwait(false);

        wrapper.CommandsChanged -= CommandProvider_CommandsChanged;
        wrapper.CommandsChanged += CommandProvider_CommandsChanged;

        return result;
    }

    private void CommandProvider_CommandsChanged(CommandProviderWrapper sender, IItemsChangedEventArgs args) =>
        _ = Task.Run(async () => await UpdateCommandsForProviderAsync(sender));

    private async Task UpdateCommandsForProviderAsync(CommandProviderWrapper sender)
    {
        var objectSets = await LoadTopLevelCommandsFromProviderAsync(sender).ConfigureAwait(false);
        var newItems = CombineTopLevelObjectSets(objectSets);

        await _topLevelCommandsLock.WaitAsync().ConfigureAwait(false);
        try
        {
            List<TopLevelViewModel> removed = [];

            foreach (var cmd in _topLevelCommands)
            {
                if (cmd.CommandProviderId == sender.ProviderId)
                {
                    removed.Add(cmd);
                }
            }

            foreach (var cmd in removed)
            {
                _topLevelCommands.Remove(cmd);
            }

            if (removed.Count > 0)
            {
                OnCommandsRemoved?.Invoke(sender, removed);
            }

            foreach (var item in newItems)
            {
                _topLevelCommands.Add(item);
            }
        }
        finally
        {
            _topLevelCommandsLock.Release();
        }

        if (newItems.Count > 0)
        {
            OnCommandsAdded?.Invoke(sender, newItems);
        }
    }

    private static List<TopLevelViewModel> CombineTopLevelObjectSets(TopLevelObjectSets objectSets)
    {
        List<TopLevelViewModel> items = [];

        if (objectSets.Commands is not null)
        {
            items.AddRange(objectSets.Commands);
        }

        if (objectSets.DockBands is not null)
        {
            items.AddRange(objectSets.DockBands);
        }

        return items;
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Package catalog event handlers (hot-install / uninstall)
    // ═══════════════════════════════════════════════════════════════════
    private void Catalog_PackageInstalling(PackageCatalog sender, PackageInstallingEventArgs args)
    {
        if (args.IsComplete)
        {
            lock (_catalogLock)
            {
                InstallPackageUnderLock(args.Package);
            }
        }
    }

    private void Catalog_PackageUninstalling(PackageCatalog sender, PackageUninstallingEventArgs args)
    {
        if (args.IsComplete)
        {
            lock (_catalogLock)
            {
                UninstallPackageUnderLock(args.Package);
            }
        }
    }

    private void Catalog_PackageUpdating(PackageCatalog sender, PackageUpdatingEventArgs args)
    {
        if (args.IsComplete)
        {
            lock (_catalogLock)
            {
                UninstallPackageUnderLock(args.TargetPackage);
                InstallPackageUnderLock(args.TargetPackage);
            }
        }
    }

    private void InstallPackageUnderLock(Package package)
    {
        var result = Task.Run(() => IsValidCmdPalExtensionAsync(package)).Result;

        if (!result.IsExtension || result.Extension is null)
        {
            return;
        }

        LogExtensionInstalled(result.Extension.DisplayName);

        _ = Task.Run(async () =>
        {
            var ct = _loadCts.Token;
            var wrapperLogger = _loggerFactory.CreateLogger<CommandProviderWrapper>();
            var extensionWrappers = await CreateExtensionWrappersAsync(result.Extension).ConfigureAwait(false);

            await _extensionsLock.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                _installedExtensions.AddRange(extensionWrappers);
            }
            finally
            {
                _extensionsLock.Release();
            }

            await StartExtensionsAndGetCommandsAsync(extensionWrappers, ct).ConfigureAwait(false);
        });
    }

    private void UninstallPackageUnderLock(Package package)
    {
        List<IExtensionWrapper> removedExtensions = [];

        // Snapshot under _extensionsLock to avoid concurrent modification
        // from DiscoverInstalledExtensionsAsync or InstallPackageUnderLock.
        var extensionsSnapshot = _installedExtensions.ToList();

        foreach (var extension in extensionsSnapshot)
        {
            if (extension.PackageFullName == package.Id.FullName)
            {
                LogExtensionUninstalled(extension.PackageDisplayName);
                removedExtensions.Add(extension);
            }
        }

        if (removedExtensions.Count == 0)
        {
            return;
        }

        _ = Task.Run(async () =>
        {
            // Remove from installed extensions list.
            await _extensionsLock.WaitAsync().ConfigureAwait(false);
            try
            {
                _installedExtensions.RemoveAll(i => removedExtensions.Contains(i));
            }
            finally
            {
                _extensionsLock.Release();
            }

            // Find wrappers that belong to the removed extensions and remove them.
            var removedWrappers = new List<CommandProviderWrapper>();

            await _allWrappersLock.WaitAsync().ConfigureAwait(false);
            await _enabledWrappersLock.WaitAsync().ConfigureAwait(false);
            try
            {
                foreach (var removedExt in removedExtensions)
                {
                    foreach (var w in _allWrappers)
                    {
                        if (w.Extension?.ExtensionUniqueId == removedExt.ExtensionUniqueId)
                        {
                            removedWrappers.Add(w);
                        }
                    }

                    _allWrappers.RemoveAll(w => w.Extension?.ExtensionUniqueId == removedExt.ExtensionUniqueId);
                    _enabledWrappers.RemoveAll(w => w.Extension?.ExtensionUniqueId == removedExt.ExtensionUniqueId);
                }
            }
            finally
            {
                _allWrappersLock.Release();
                _enabledWrappersLock.Release();
            }

            // Remove commands belonging to the removed wrappers.
            var removedWrapperCommands = new List<(CommandProviderWrapper Wrapper, List<TopLevelViewModel> Items)>();

            await _topLevelCommandsLock.WaitAsync().ConfigureAwait(false);
            try
            {
                foreach (var wrapper in removedWrappers)
                {
                    List<TopLevelViewModel> items = [];

                    foreach (var cmd in _topLevelCommands)
                    {
                        if (cmd.CommandProviderId == wrapper.ProviderId)
                        {
                            items.Add(cmd);
                        }
                    }

                    _topLevelCommands.RemoveAll(c => c.CommandProviderId == wrapper.ProviderId);
                    removedWrapperCommands.Add((wrapper, items));
                }
            }
            finally
            {
                _topLevelCommandsLock.Release();
            }

            // Fire events after releasing all locks.
            if (removedWrappers.Count > 0)
            {
                OnCommandProviderRemoved?.Invoke(this, removedWrappers);
            }

            foreach (var (wrapper, items) in removedWrapperCommands)
            {
                if (items.Count > 0)
                {
                    OnCommandsRemoved?.Invoke(wrapper, items);
                }
            }
        });
    }

    private static async Task<IsExtensionResult> IsValidCmdPalExtensionAsync(Package package)
    {
        var extensions = await AppExtensionCatalog
            .Open("com.microsoft.commandpalette")
            .FindAllAsync();

        foreach (var extension in extensions)
        {
            if (package.Id?.FullName == extension.Package?.Id?.FullName)
            {
                var (cmdPalProvider, classIds) = await GetCmdPalExtensionPropertiesAsync(extension).ConfigureAwait(false);
                return new(cmdPalProvider is not null && classIds.Count != 0, extension);
            }
        }

        return new(false, null);
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Property-set helpers (shared with Models/ExtensionService.cs)
    // ═══════════════════════════════════════════════════════════════════
    private static IPropertySet? GetSubPropertySet(IPropertySet propSet, string name) =>
        propSet.TryGetValue(name, out var value) ? value as IPropertySet : null;

    private static object[]? GetSubPropertySetArray(IPropertySet propSet, string name) =>
        propSet.TryGetValue(name, out var value) ? value as object[] : null;

    private static string? GetProperty(IPropertySet propSet, string name) =>
        propSet[name] as string;

    /// <summary>
    /// Handles extensions that declare one or many COM class instances in their
    /// manifest's <c>Activation/CreateInstance</c> element.
    /// </summary>
    private static List<string> GetCreateInstanceList(IPropertySet activationPropSet)
    {
        var propSetList = new List<string>();
        var singlePropertySet = GetSubPropertySet(activationPropSet, CreateInstanceProperty);

        if (singlePropertySet is not null)
        {
            var classId = GetProperty(singlePropertySet, ClassIdProperty);
            if (classId is not null)
            {
                propSetList.Add(classId);
            }
        }
        else
        {
            var propertySetArray = GetSubPropertySetArray(activationPropSet, CreateInstanceProperty);
            if (propertySetArray is not null)
            {
                foreach (var prop in propertySetArray)
                {
                    if (prop is not IPropertySet propertySet)
                    {
                        continue;
                    }

                    var classId = GetProperty(propertySet, ClassIdProperty);
                    if (classId is not null)
                    {
                        propSetList.Add(classId);
                    }
                }
            }
        }

        return propSetList;
    }

    // ═══════════════════════════════════════════════════════════════════
    //  Dispose
    // ═══════════════════════════════════════════════════════════════════
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        _loadCts.Cancel();
        _loadCts.Dispose();
        _extensionsLock.Dispose();
        _allWrappersLock.Dispose();
        _enabledWrappersLock.Dispose();
        _topLevelCommandsLock.Dispose();

        GC.SuppressFinalize(this);
    }

    // ═══════════════════════════════════════════════════════════════════
    //  LoggerMessage source-generated methods
    // ═══════════════════════════════════════════════════════════════════
    [LoggerMessage(Level = LogLevel.Debug, Message = "Starting extension {PackageFullName}")]
    partial void LogStartingExtension(string packageFullName);

    [LoggerMessage(Level = LogLevel.Information, Message = "Started extension {PackageFullName} in {ElapsedMs}ms")]
    partial void LogExtensionStarted(string packageFullName, long elapsedMs);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Starting extension {PackageFullName} timed out after {ElapsedMs}ms, continuing in background")]
    partial void LogExtensionStartTimedOut(string packageFullName, long elapsedMs);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Starting extension {PackageFullName} was cancelled after {ElapsedMs}ms")]
    partial void LogExtensionStartCancelled(string packageFullName, long elapsedMs);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to start extension {PackageFullName} after {ElapsedMs}ms")]
    partial void LogExtensionStartFailed(Exception exception, string packageFullName, long elapsedMs);

    [LoggerMessage(Level = LogLevel.Information, Message = "Late-started extension {PackageFullName} in {ElapsedMs}ms, loading commands")]
    partial void LogExtensionLateStarted(string packageFullName, long elapsedMs);

    [LoggerMessage(Level = LogLevel.Error, Message = "Background start of extension {PackageFullName} failed after {ElapsedMs}ms")]
    partial void LogBackgroundStartFailed(Exception exception, string packageFullName, long elapsedMs);

    [LoggerMessage(Level = LogLevel.Information, Message = "Loaded {CommandCount} command(s) and {BandCount} band(s) from {PackageFullName} in {ElapsedMs}ms")]
    partial void LogCommandsLoaded(string? packageFullName, int commandCount, int bandCount, long elapsedMs);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Loading commands from {PackageFullName} timed out after {ElapsedMs}ms, continuing in background")]
    partial void LogCommandLoadTimedOut(string? packageFullName, long elapsedMs);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to load commands from {PackageFullName} after {ElapsedMs}ms")]
    partial void LogCommandLoadFailed(Exception exception, string? packageFullName, long elapsedMs);

    [LoggerMessage(Level = LogLevel.Information, Message = "Late-loaded {CommandCount} command(s) and {BandCount} band(s) from {PackageFullName} in {ElapsedMs}ms")]
    partial void LogCommandsLateLoaded(string? packageFullName, int commandCount, int bandCount, long elapsedMs);

    [LoggerMessage(Level = LogLevel.Error, Message = "Background loading from {PackageFullName} failed after {ElapsedMs}ms")]
    partial void LogBackgroundCommandLoadFailed(Exception exception, string? packageFullName, long elapsedMs);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Completed loading WinRT extensions in {ElapsedMs}ms")]
    partial void LogLoadingExtensionsCompleted(long elapsedMs);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Signaling dispose to {ExtensionUniqueId}")]
    partial void LogDisposingExtension(string extensionUniqueId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to send dispose signal to extension {ExtensionUniqueId}")]
    partial void LogFailedSendingDispose(Exception exception, string extensionUniqueId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Installed new extension app {ExtensionName}")]
    partial void LogExtensionInstalled(string extensionName);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Uninstalled extension app {PackageName}")]
    partial void LogExtensionUninstalled(string packageName);

    // ═══════════════════════════════════════════════════════════════════
    //  Inner types
    // ═══════════════════════════════════════════════════════════════════
    private sealed class ExtensionStartResult
    {
        public IExtensionWrapper Extension { get; }

        public CommandProviderWrapper? Wrapper { get; private init; }

        public Task? PendingStartTask { get; private init; }

        public Stopwatch? Stopwatch { get; private init; }

        [MemberNotNullWhen(true, nameof(Wrapper))]
        public bool IsStarted => Wrapper is not null;

        [MemberNotNullWhen(true, nameof(PendingStartTask), nameof(Stopwatch))]
        public bool IsTimedOut => PendingStartTask is not null;

        private ExtensionStartResult(IExtensionWrapper extension)
        {
            Extension = extension;
        }

        public static ExtensionStartResult Started(IExtensionWrapper extension, CommandProviderWrapper wrapper) =>
            new(extension) { Wrapper = wrapper };

        public static ExtensionStartResult TimedOut(IExtensionWrapper extension, Task pendingStartTask, Stopwatch sw) =>
            new(extension) { PendingStartTask = pendingStartTask, Stopwatch = sw };

        public static ExtensionStartResult Failed(IExtensionWrapper extension) =>
            new(extension);
    }

    private sealed class CommandLoadResult
    {
        public TopLevelObjectSets? TopLevelObjectSets { get; private init; }

        public CommandProviderWrapper Wrapper { get; }

        public Task<TopLevelObjectSets>? PendingLoadTask { get; private init; }

        public Stopwatch? Stopwatch { get; private init; }

        [MemberNotNullWhen(true, nameof(TopLevelObjectSets))]
        public bool IsLoaded => TopLevelObjectSets is not null;

        [MemberNotNullWhen(true, nameof(PendingLoadTask), nameof(Stopwatch))]
        public bool IsTimedOut => PendingLoadTask is not null;

        private CommandLoadResult(CommandProviderWrapper wrapper)
        {
            Wrapper = wrapper;
        }

        public static CommandLoadResult Loaded(CommandProviderWrapper wrapper, TopLevelObjectSets objectSets) =>
            new(wrapper) { TopLevelObjectSets = objectSets };

        public static CommandLoadResult TimedOut(CommandProviderWrapper wrapper, Task<TopLevelObjectSets> pendingLoadTask, Stopwatch sw) =>
            new(wrapper) { PendingLoadTask = pendingLoadTask, Stopwatch = sw };

        public static CommandLoadResult Failed(CommandProviderWrapper wrapper) =>
            new(wrapper);
    }

    private record TopLevelObjectSets(ICollection<TopLevelViewModel>? Commands, ICollection<TopLevelViewModel>? DockBands);

    private record struct IsExtensionResult(bool IsExtension, AppExtension? Extension);
}
