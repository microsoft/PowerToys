// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using Microsoft.CmdPal.UI.ViewModels.Models;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.Extensions.Logging;
using Windows.ApplicationModel;
using Windows.ApplicationModel.AppExtensions;
using Windows.Foundation;
using Windows.Foundation.Collections;

namespace Microsoft.CmdPal.UI.ViewModels.Services;

public partial class WinRTExtensionService : IExtensionService, IDisposable
{
    public event TypedEventHandler<IExtensionService, IEnumerable<CommandProviderWrapper>>? OnCommandProviderAdded;

    public event TypedEventHandler<IExtensionService, IEnumerable<CommandProviderWrapper>>? OnCommandProviderRemoved;

    public event TypedEventHandler<CommandProviderWrapper, IEnumerable<TopLevelViewModel>>? OnCommandsAdded;

    public event TypedEventHandler<CommandProviderWrapper, IEnumerable<TopLevelViewModel>>? OnCommandsRemoved;

    private readonly ILogger _logger;
    private readonly SettingsService _settingsService;
    private readonly TaskScheduler _taskScheduler;
    private readonly HotkeyManager _hotkeyManager;
    private readonly AliasManager _aliasManager;

    private bool _disposedValue;

    private const string CreateInstanceProperty = "CreateInstance";
    private const string ClassIdProperty = "@ClassId";

    private static readonly PackageCatalog _catalog = PackageCatalog.OpenForCurrentUser();
    private static readonly Lock _catalogLock = new();

    private readonly List<IExtensionWrapper> _installedExtensions = [];
    private readonly SemaphoreSlim _getInstalledExtensionsLock = new(1, 1);
    private readonly SemaphoreSlim _getInstalledWidgetsLock = new(1, 1);

    private readonly IEnumerable<ICommandProvider> _commandProviders = [];
    private readonly Lock _commandProvidersLock = new();

    private readonly List<CommandProviderWrapper> _installedCommandProviderWrappers = [];
    private readonly SemaphoreSlim _getInstalledCommandWrappersLock = new(1, 1);

    private readonly List<CommandProviderWrapper> _enabledCommandWrappers = [];
    private readonly SemaphoreSlim _getEnabledCommandWrappersLock = new(1, 1);

    private readonly List<TopLevelViewModel> _topLevelCommands = [];
    private readonly SemaphoreSlim _getTopLevelCommandsLock = new(1, 1);

    private WeakReference<IPageContext>? _weakPageContext;

    public WinRTExtensionService(
        SettingsService settingsService,
        TaskScheduler taskScheduler,
        HotkeyManager hotkeyManager,
        AliasManager aliasManager,
        ILogger logger)
    {
        _logger = logger;
        _settingsService = settingsService;
        _taskScheduler = taskScheduler;
        _hotkeyManager = hotkeyManager;
        _aliasManager = aliasManager;

        _catalog.PackageInstalling += Catalog_PackageInstalling;
        _catalog.PackageUninstalling += Catalog_PackageUninstalling;
        _catalog.PackageUpdating += Catalog_PackageUpdating;
    }

    public async Task SignalStartExtensionsAsync(WeakReference<IPageContext> weakPageContext)
    {
        _weakPageContext = weakPageContext;

        var timer = new Stopwatch();
        timer.Start();

        await GetInstalledExtensionsAsync();

        // Start all extensions in parallel
        IEnumerable<Task> startTasks = [];
        await _getInstalledExtensionsLock.WaitAsync();
        try
        {
            startTasks = _installedExtensions.Select(StartExtensionAndGetCommands);
        }
        finally
        {
            _getInstalledExtensionsLock.Release();
        }

        await Task.WhenAll(startTasks);

        timer.Stop();
        Log_LoadingExtensionsCompleted(timer.ElapsedMilliseconds);
    }

    public async Task SignalStopExtensionsAsync()
    {
        var installedExtensions = await GetInstalledExtensionsAsync();
        foreach (var installedExtension in installedExtensions)
        {
            Log_DisposingExtension(installedExtension.ExtensionUniqueId);
            try
            {
                if (installedExtension.IsRunning())
                {
                    installedExtension.SignalDispose();
                }
            }
            catch (Exception ex)
            {
                Log_FailedSendingDisposeToExtension(ex, installedExtension.ExtensionUniqueId);
            }
        }
    }

    public async Task DisableProviderAsync(string providerId)
    {
        await _getEnabledCommandWrappersLock.WaitAsync();
        try
        {
            CommandProviderWrapper? wrapper = null;

            foreach (var enabledWrapper in _enabledCommandWrappers)
            {
                if (enabledWrapper.Id.Equals(providerId, StringComparison.Ordinal))
                {
                    wrapper = enabledWrapper;
                }
            }

            if (wrapper != null)
            {
                _enabledCommandWrappers.Remove(wrapper);

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
            _getEnabledCommandWrappersLock.Release();
        }
    }

    public async Task EnableProviderAsync(string providerId)
    {
        await _getEnabledCommandWrappersLock.WaitAsync();
        try
        {
            foreach (var wrapper in _enabledCommandWrappers)
            {
                if (wrapper.Id.Equals(providerId, StringComparison.Ordinal))
                {
                    return;
                }
            }
        }
        finally
        {
            _getEnabledCommandWrappersLock.Release();
        }

        await _getInstalledCommandWrappersLock.WaitAsync();
        try
        {
            CommandProviderWrapper? wrapper = null;

            foreach (var builtInWrapper in _installedCommandProviderWrappers)
            {
                if (builtInWrapper.Id.Equals(providerId, StringComparison.Ordinal))
                {
                    wrapper = builtInWrapper;
                }
            }

            if (wrapper != null)
            {
                await _getEnabledCommandWrappersLock.WaitAsync();
                try
                {
                    _enabledCommandWrappers.Add(wrapper);
                }
                finally
                {
                    _getEnabledCommandWrappersLock.Release();
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
            _getInstalledCommandWrappersLock.Release();
        }
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposedValue)
        {
            if (disposing)
            {
                _getInstalledExtensionsLock.Dispose();
                _getInstalledWidgetsLock.Dispose();
            }

            _disposedValue = true;
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

    private async Task StartExtensionAndGetCommands(IExtensionWrapper extensionWrapper)
    {
        var providerWrapper = await StartExtensionWithTimeoutAsync(extensionWrapper);

        if (providerWrapper is not null)
        {
            await _getInstalledCommandWrappersLock.WaitAsync();
            try
            {
                _installedCommandProviderWrappers.Add(providerWrapper);
            }
            finally
            {
                _getInstalledCommandWrappersLock.Release();
            }

            // Load the commands from the providers in parallel
            var commandSets = await LoadCommandsWithTimeoutAsync(providerWrapper);

            OnCommandProviderAdded?.Invoke(this, new[] { providerWrapper });

            if (commandSets.TopLevelViewModels is not null)
            {
                var addedCommands = new List<TopLevelViewModel>();
                await _getTopLevelCommandsLock.WaitAsync();
                try
                {
                    foreach (var c in commandSets.TopLevelViewModels!)
                    {
                        addedCommands.Add(c);
                        _topLevelCommands.Add(c);
                    }
                }
                finally
                {
                    _getTopLevelCommandsLock.Release();
                }

                OnCommandsAdded?.Invoke(providerWrapper, addedCommands);
            }
        }
    }

    private async Task<(CommandProviderWrapper ProviderWrapper, IEnumerable<TopLevelViewModel>? TopLevelViewModels)> LoadCommandsWithTimeoutAsync(CommandProviderWrapper wrapper)
    {
        try
        {
            var topLevelViewModels = await LoadTopLevelCommandsFromProvider(wrapper!).WaitAsync(TimeSpan.FromSeconds(10));
            return (wrapper!, topLevelViewModels);
        }
        catch (TimeoutException)
        {
            Log_LoadingCommandsTimedOut(wrapper!.ExtensionHost?.Extension?.PackageFullName);
        }
        catch (Exception ex)
        {
            Log_FailedToLoadCommands(wrapper!.ExtensionHost?.Extension?.PackageFullName, ex);
        }

        return (wrapper, null);
    }

    private async Task<CommandProviderWrapper?> StartExtensionWithTimeoutAsync(IExtensionWrapper extension)
    {
        Log_StartingExtension(extension.PackageFullName);
        try
        {
            await extension.StartExtensionAsync().WaitAsync(TimeSpan.FromSeconds(10));
            return new CommandProviderWrapper(extension, _taskScheduler, _hotkeyManager, _aliasManager, _logger);
        }
        catch (Exception ex)
        {
            Log_ExtensionFailedToStart(extension.PackageFullName, ex);
            return null; // Return null for failed extensions
        }
    }

    /// <summary>
    /// Called when a command provider raises its ItemsChanged event. We'll
    /// remove the old commands from the top-level list and try to put the new
    /// ones in the same place in the list.
    /// </summary>
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

        // Modify the TopLevelCommands under shared lock; event if we clone it, we don't want
        // TopLevelCommands to get modified while we're working on it. Otherwise, we might
        // out clone would be stale at the end of this method.
        await _getTopLevelCommandsLock.WaitAsync();
        try
        {
            // Work on a clone of the list, so that we can just do one atomic
            // update to the actual observable list at the end
            // TODO: just added a lock around all of this anyway, but keeping the clone
            // while looking on some other ways to improve this; can be removed later
            // .
            // The clone will be everything except the commands
            // from the provider that raised the event
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

    /* BELOW ARE PACKAGE METHODS */

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
                // Remove any extension providers that we previously had from this app
                UninstallPackageUnderLock(args.TargetPackage);

                // then add the new ones.
                InstallPackageUnderLock(args.TargetPackage);
            }
        }
    }

    private void InstallPackageUnderLock(Package package)
    {
        var isCmdPalExtensionResult = Task.Run(() =>
        {
            return IsValidCmdPalExtension(package);
        }).Result;
        var isExtension = isCmdPalExtensionResult.IsExtension;
        var extension = isCmdPalExtensionResult.Extension;
        if (isExtension && extension is not null)
        {
            Log_ExtensionInstalled(extension.DisplayName);

            Task.Run(async () =>
            {
                var extensionWrappers = await CreateWrappersForExtension(extension);
                await _getInstalledExtensionsLock.WaitAsync();
                try
                {
                    await UpdateExtensionsListsFromWrappers(extensionWrappers);
                }
                finally
                {
                    _getInstalledExtensionsLock.Release();
                }

                // Start all providers in parallel
                IEnumerable<Task> startTasks = extensionWrappers.Select(StartExtensionAndGetCommands);
                await Task.WhenAll(startTasks);
            });
        }
    }

    private void UninstallPackageUnderLock(Package package)
    {
        List<IExtensionWrapper> removedExtensions = [];
        foreach (var extension in _installedExtensions)
        {
            if (extension.PackageFullName == package.Id.FullName)
            {
                Log_ExtensionUninstalled(extension.PackageDisplayName);

                removedExtensions.Add(extension);
            }
        }

        Task.Run(async () =>
        {
            // Clear out extensions
            await _getInstalledExtensionsLock.WaitAsync();
            try
            {
                await _getInstalledExtensionsLock.WaitAsync();
                try
                {
                    _installedExtensions.RemoveAll(i => removedExtensions.Contains(i));
                }
                finally
                {
                    _getInstalledExtensionsLock.Release();
                }
            }
            finally
            {
                _getInstalledExtensionsLock.Release();
            }

            // Clear out any wrappers & commands from the removed extensions, and get a list of them to signal the UI about after.
            var removedWrappers = new List<CommandProviderWrapper>();

            await _getInstalledCommandWrappersLock.WaitAsync();
            await _getEnabledCommandWrappersLock.WaitAsync();
            try
            {
                foreach (var removedExtension in removedExtensions)
                {
                    removedWrappers.AddRange(_installedCommandProviderWrappers.Where(w => w.Extension?.ExtensionUniqueId == removedExtension.ExtensionUniqueId).ToList());
                    _installedCommandProviderWrappers.RemoveAll(w => w.Extension?.ExtensionUniqueId == removedExtension.ExtensionUniqueId);
                    _enabledCommandWrappers.RemoveAll(w => w.Extension?.ExtensionUniqueId == removedExtension.ExtensionUniqueId);
                }
            }
            finally
            {
                _getInstalledCommandWrappersLock.Release();
                _getEnabledCommandWrappersLock.Release();
            }

            var removedWrapperCommands = new List<(CommandProviderWrapper CommandProvider, List<TopLevelViewModel> TopLevelViewModels)>();
            await _getTopLevelCommandsLock.WaitAsync();
            try
            {
                foreach (var removedWrapper in removedWrappers)
                {
                    removedWrapperCommands.AddRange((removedWrapper, _topLevelCommands.Where(c => c.CommandProviderId == removedWrapper.Id).ToList()));
                    _topLevelCommands.RemoveAll(c => c.CommandProviderId == removedWrapper.Id);
                }
            }
            finally
            {
                _getTopLevelCommandsLock.Release();
            }

            OnCommandProviderRemoved?.Invoke(this, removedWrappers);

            foreach (var wrapperCommands in removedWrapperCommands)
            {
                OnCommandsRemoved?.Invoke(wrapperCommands.CommandProvider, wrapperCommands.TopLevelViewModels);
            }
        });
    }

    private static async Task<IsExtensionResult> IsValidCmdPalExtension(Package package)
    {
        var extensions = await AppExtensionCatalog.Open("com.microsoft.commandpalette").FindAllAsync();
        foreach (var extension in extensions)
        {
            if (package.Id?.FullName == extension.Package?.Id?.FullName)
            {
                var (cmdPalProvider, classId) = await GetCmdPalExtensionPropertiesAsync(extension);

                return new(cmdPalProvider is not null && classId.Count != 0, extension);
            }
        }

        return new(false, null);
    }

    /* BELOW ARE EXTENSION COMMUNICATION METHODS */

    private async Task<IEnumerable<IExtensionWrapper>> GetInstalledExtensionsAsync()
    {
        await _getInstalledExtensionsLock.WaitAsync();
        try
        {
            if (_installedExtensions.Count == 0)
            {
                var extensions = await GetInstalledAppExtensionsAsync();
                foreach (var extension in extensions)
                {
                    var wrappers = await CreateWrappersForExtension(extension);
                    await UpdateExtensionsListsFromWrappers(wrappers);
                }
            }

            return _installedExtensions;
        }
        finally
        {
            _getInstalledExtensionsLock.Release();
        }
    }

    private static async Task<IEnumerable<AppExtension>> GetInstalledAppExtensionsAsync() => await AppExtensionCatalog.Open("com.microsoft.commandpalette").FindAllAsync();

    private async Task<List<ExtensionWrapper>> CreateWrappersForExtension(AppExtension extension)
    {
        var (cmdPalProvider, classIds) = await GetCmdPalExtensionPropertiesAsync(extension);

        if (cmdPalProvider is null || classIds.Count == 0)
        {
            return [];
        }

        List<ExtensionWrapper> wrappers = [];
        foreach (var classId in classIds)
        {
            var extensionWrapper = CreateExtensionWrapper(extension, cmdPalProvider, classId);
            wrappers.Add(extensionWrapper);
        }

        return wrappers;
    }

    private static async Task<(IPropertySet? CmdPalProvider, List<string> ClassIds)> GetCmdPalExtensionPropertiesAsync(AppExtension extension)
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

        // Handle case where extension creates multiple instances.
        classIds.AddRange(GetCreateInstanceList(activation));

        return (cmdPalProvider, classIds);
    }

    private ExtensionWrapper CreateExtensionWrapper(AppExtension extension, IPropertySet cmdPalProvider, string classId)
    {
        var extensionWrapper = new ExtensionWrapper(extension, classId);

        var supportedInterfaces = GetSubPropertySet(cmdPalProvider, "SupportedInterfaces");
        if (supportedInterfaces is not null)
        {
            foreach (var supportedInterface in supportedInterfaces)
            {
                ProviderType pt;
                if (Enum.TryParse(supportedInterface.Key, out pt))
                {
                    extensionWrapper.AddProviderType(pt);
                }
                else
                {
                    // log warning  that extension declared unsupported extension interface
                    Log_UndeclaredInterfaceInExtension(extension.DisplayName, supportedInterface.Key);
                }
            }
        }

        return extensionWrapper;
    }

    private async Task UpdateExtensionsListsFromWrappers(List<ExtensionWrapper> wrappers)
    {
        foreach (var extensionWrapper in wrappers)
        {
            _installedExtensions.Add(extensionWrapper);
        }
    }

    private static IPropertySet? GetSubPropertySet(IPropertySet propSet, string name) => propSet.TryGetValue(name, out var value) ? value as IPropertySet : null;

    private static object[]? GetSubPropertySetArray(IPropertySet propSet, string name) => propSet.TryGetValue(name, out var value) ? value as object[] : null;

    private static List<string> GetCreateInstanceList(IPropertySet activationPropSet)
    {
        var propSetList = new List<string>();
        var singlePropertySet = GetSubPropertySet(activationPropSet, CreateInstanceProperty);
        if (singlePropertySet is not null)
        {
            var classId = GetProperty(singlePropertySet, ClassIdProperty);

            // If the instance has a classId as a single string, then it's only supporting a single instance.
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

    private static string? GetProperty(IPropertySet propSet, string name) => propSet[name] as string;

    [LoggerMessage(
        Level = LogLevel.Debug,
        Message = "Installed new extension app {extensionName}")]
    partial void Log_ExtensionInstalled(string extensionName);

    [LoggerMessage(
        Level = LogLevel.Debug,
        Message = "Uninstalled extension app {extensionPackageName}")]
    partial void Log_ExtensionUninstalled(string extensionPackageName);

    [LoggerMessage(
        Level = LogLevel.Debug,
        Message = "Extension {extensionName} declared an unsupported interface: {InterfaceKey}")]
    partial void Log_UndeclaredInterfaceInExtension(string extensionName, string interfaceKey);

    [LoggerMessage(
        Level = LogLevel.Debug,
        Message = "Signaling dispose to {extensionUniqueId}")]
    partial void Log_DisposingExtension(string extensionUniqueId);

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Failed to send dispose signal to extension {extensionUniqueId}")]
    partial void Log_FailedSendingDisposeToExtension(Exception ex, string extensionUniqueId);

    [LoggerMessage(
        Level = LogLevel.Debug,
        Message = "Completed loading WinRT extensions in {elapsedMilliseconds}ms")]
    partial void Log_LoadingExtensionsCompleted(long elapsedMilliseconds);

    [LoggerMessage(
        Level = LogLevel.Debug,
        Message = "Starting extension {packageFullName}")]
    partial void Log_StartingExtension(string packageFullName);

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Extension {packageFullName} failed to start.")]
    partial void Log_ExtensionFailedToStart(string packageFullName, Exception exception);

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Loading commands from {packageFullName} timed out")]
    partial void Log_LoadingCommandsTimedOut(string? packageFullName);

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Failed to load commands for extension {packageFullName}")]
    partial void Log_FailedToLoadCommands(string? packageFullName, Exception exception);
}

internal record struct IsExtensionResult(bool IsExtension, AppExtension? Extension)
{
}
