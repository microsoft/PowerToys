// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Immutable;
using System.Diagnostics;
using ManagedCommon;
using Microsoft.CmdPal.Common.Services;
using Microsoft.CmdPal.UI.ViewModels.Models;
using Microsoft.CommandPalette.Extensions;
using Windows.ApplicationModel;
using Windows.ApplicationModel.AppExtensions;
using Windows.Foundation;
using Windows.Foundation.Collections;

namespace Microsoft.CmdPal.UI.ViewModels.Services;

/// <summary>
/// Extension service that manages out-of-process WinRT AppExtension-based command providers.
/// Handles package catalog monitoring, extension startup with timeouts, and background retries.
/// </summary>
public partial class WinRTExtensionService : IExtensionService, IDisposable
{
    private static readonly TimeSpan ExtensionStartTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan BackgroundStartTimeout = TimeSpan.FromSeconds(60);

    private static readonly PackageCatalog _catalog = PackageCatalog.OpenForCurrentUser();
    private readonly SemaphoreSlim _getInstalledExtensionsLock = new(1, 1);
    private readonly TaskScheduler _taskScheduler;
    private readonly ICommandProviderCache _commandProviderCache;

    private bool _disposedValue;

    private const string CreateInstanceProperty = "CreateInstance";
    private const string ClassIdProperty = "@ClassId";

    private static readonly List<IExtensionWrapper> _installedExtensions = [];
    private static readonly List<IExtensionWrapper> _enabledExtensions = [];

    public event TypedEventHandler<IExtensionService, IEnumerable<CommandProviderWrapper>>? OnProviderAdded;

    public event TypedEventHandler<IExtensionService, IEnumerable<CommandProviderWrapper>>? OnProviderRemoved;

    public WinRTExtensionService(TaskScheduler taskScheduler, ICommandProviderCache commandProviderCache)
    {
        _taskScheduler = taskScheduler;
        _commandProviderCache = commandProviderCache;

        _catalog.PackageInstalling += Catalog_PackageInstalling;
        _catalog.PackageUninstalling += Catalog_PackageUninstalling;
        _catalog.PackageUpdating += Catalog_PackageUpdating;
    }

    public async Task<IEnumerable<CommandProviderWrapper>> LoadProvidersAsync(CancellationToken ct)
    {
        var extensions = (await GetInstalledExtensionsAsync().ConfigureAwait(false)).ToImmutableList();

        var timer = Stopwatch.StartNew();

        // Start all extensions in parallel
        var startResults = await Task.WhenAll(extensions.Select(ext => TryStartExtensionAsync(ext, ct))).ConfigureAwait(false);

        var startedWrappers = new List<CommandProviderWrapper>();
        foreach (var r in startResults)
        {
            if (r.IsStarted)
            {
                startedWrappers.Add(r.Wrapper);
            }
            else if (r.IsTimedOut)
            {
                _ = StartExtensionWhenReadyAsync(r.Extension, r.PendingStartTask, r.Stopwatch, ct);
            }
        }

        timer.Stop();
        Logger.LogInfo($"WinRTExtensionService: Started {startedWrappers.Count} extension(s) in {timer.ElapsedMilliseconds} ms");

        return startedWrappers;
    }

    public async Task SignalStopAsync()
    {
        var installedExtensions = await GetInstalledExtensionsAsync().ConfigureAwait(false);
        foreach (var installedExtension in installedExtensions)
        {
            Logger.LogDebug($"Signaling dispose to {installedExtension.ExtensionUniqueId}");
            try
            {
                if (installedExtension.IsRunning())
                {
                    installedExtension.SignalDispose();
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to send dispose signal to extension {installedExtension.ExtensionUniqueId}", ex);
            }
        }
    }

    private async Task<ExtensionStartResult> TryStartExtensionAsync(IExtensionWrapper extension, CancellationToken ct)
    {
        Logger.LogDebug($"Starting {extension.PackageFullName}");
        var sw = Stopwatch.StartNew();
        var startTask = extension.StartExtensionAsync();
        try
        {
            await startTask.WaitAsync(ExtensionStartTimeout, ct).ConfigureAwait(false);
            Logger.LogInfo($"Started extension {extension.PackageFullName} in {sw.ElapsedMilliseconds} ms");
            return ExtensionStartResult.Started(extension, new CommandProviderWrapper(extension, _taskScheduler, _commandProviderCache));
        }
        catch (TimeoutException)
        {
            Logger.LogWarning($"Starting extension {extension.PackageFullName} timed out after {sw.ElapsedMilliseconds} ms, continuing in background");
            return ExtensionStartResult.TimedOut(extension, startTask, sw);
        }
        catch (OperationCanceledException)
        {
            Logger.LogDebug($"Starting extension {extension.PackageFullName} was cancelled after {sw.ElapsedMilliseconds} ms");
            return ExtensionStartResult.Failed(extension);
        }
        catch (Exception ex)
        {
            Logger.LogError($"Failed to start extension {extension.PackageFullName} after {sw.ElapsedMilliseconds} ms: {ex}");
            return ExtensionStartResult.Failed(extension);
        }
    }

    private async Task StartExtensionWhenReadyAsync(
        IExtensionWrapper extension,
        Task startTask,
        Stopwatch sw,
        CancellationToken ct)
    {
        try
        {
            await startTask.WaitAsync(BackgroundStartTimeout, ct).ConfigureAwait(false);

            var wrapper = new CommandProviderWrapper(extension, _taskScheduler, _commandProviderCache);
            Logger.LogInfo($"Late-started extension {extension.PackageFullName} in {sw.ElapsedMilliseconds} ms");

            OnProviderAdded?.Invoke(this, [wrapper]);
        }
        catch (OperationCanceledException)
        {
            // Reload happened -- discard stale results
        }
        catch (Exception ex)
        {
            Logger.LogError($"Background start of extension {extension.PackageFullName} failed after {sw.ElapsedMilliseconds} ms: {ex}");
        }
    }

    private void Catalog_PackageInstalling(PackageCatalog sender, PackageInstallingEventArgs args)
    {
        if (args.IsComplete)
        {
            _ = HandlePackageInstalledAsync(args.Package);
        }
    }

    private void Catalog_PackageUninstalling(PackageCatalog sender, PackageUninstallingEventArgs args)
    {
        if (args.IsComplete)
        {
            _ = HandlePackageUninstalledAsync(args.Package);
        }
    }

    private void Catalog_PackageUpdating(PackageCatalog sender, PackageUpdatingEventArgs args)
    {
        if (args.IsComplete)
        {
            _ = HandlePackageUpdatedAsync(args.TargetPackage);
        }
    }

    private async Task HandlePackageInstalledAsync(Package package)
    {
        var result = await IsValidCmdPalExtension(package);
        if (!result.IsExtension || result.Extension is null)
        {
            return;
        }

        CommandPaletteHost.Instance.DebugLog($"Installed new extension app {result.Extension.DisplayName}");

        List<ExtensionWrapper> wrappers;
        await _getInstalledExtensionsLock.WaitAsync();
        try
        {
            wrappers = await CreateWrappersForExtension(result.Extension);
            UpdateExtensionsListsFromWrappers(wrappers);
        }
        finally
        {
            _getInstalledExtensionsLock.Release();
        }

        // Start extensions outside the lock — CoCreateInstance can be slow (OOP activation)
        await StartAndNotifyNewExtensionsAsync(wrappers, CancellationToken.None);
    }

    private async Task HandlePackageUninstalledAsync(Package package)
    {
        List<IExtensionWrapper> removedExtensions;
        await _getInstalledExtensionsLock.WaitAsync();
        try
        {
            removedExtensions = _installedExtensions
                .Where(ext => ext.PackageFullName == package.Id.FullName)
                .ToList();

            if (removedExtensions.Count == 0)
            {
                return;
            }

            foreach (var ext in removedExtensions)
            {
                CommandPaletteHost.Instance.DebugLog($"Uninstalled extension app {ext.PackageDisplayName}");
            }

            _installedExtensions.RemoveAll(i => removedExtensions.Contains(i));
            _enabledExtensions.RemoveAll(i => removedExtensions.Contains(i));
        }
        finally
        {
            _getInstalledExtensionsLock.Release();
        }

        // Build placeholder wrappers for removal notification outside the lock.
        // The TopLevelCommandManager matches by Extension reference, so we need to pass
        // something that carries the IExtensionWrapper identity.
        var removedProviders = new List<CommandProviderWrapper>();
        foreach (var ext in removedExtensions)
        {
            try
            {
                removedProviders.Add(new CommandProviderWrapper(ext, _taskScheduler, _commandProviderCache));
            }
            catch
            {
                // Extension may not be in a runnable state if it was uninstalled;
                // we still need to signal removal
            }
        }

        OnProviderRemoved?.Invoke(this, removedProviders);
    }

    private async Task HandlePackageUpdatedAsync(Package package)
    {
        await HandlePackageUninstalledAsync(package);
        await HandlePackageInstalledAsync(package);
    }

    private async Task StartAndNotifyNewExtensionsAsync(List<ExtensionWrapper> wrappers, CancellationToken ct)
    {
        var startResults = await Task.WhenAll(
            wrappers.Select(w => TryStartExtensionAsync(w, ct))).ConfigureAwait(false);

        var startedProviders = new List<CommandProviderWrapper>();
        foreach (var r in startResults)
        {
            if (r.IsStarted)
            {
                startedProviders.Add(r.Wrapper);
            }
            else if (r.IsTimedOut)
            {
                _ = StartExtensionWhenReadyAsync(r.Extension, r.PendingStartTask, r.Stopwatch, ct);
            }
        }

        if (startedProviders.Count > 0)
        {
            OnProviderAdded?.Invoke(this, startedProviders);
        }
    }

    public async Task<IEnumerable<IExtensionWrapper>> GetInstalledExtensionsAsync(bool includeDisabledExtensions = false)
    {
        await _getInstalledExtensionsLock.WaitAsync();
        try
        {
            return await GetInstalledExtensionsAsyncUnderLock(includeDisabledExtensions, refresh: false);
        }
        finally
        {
            _getInstalledExtensionsLock.Release();
        }
    }

    public async Task<IEnumerable<IExtensionWrapper>> RefreshInstalledExtensionsAsync(bool includeDisabledExtensions = false)
    {
        await _getInstalledExtensionsLock.WaitAsync();
        try
        {
            return await GetInstalledExtensionsAsyncUnderLock(includeDisabledExtensions, refresh: true);
        }
        finally
        {
            _getInstalledExtensionsLock.Release();
        }
    }

    public IExtensionWrapper? GetInstalledExtension(string extensionUniqueId)
    {
        var extension = _installedExtensions.Where(extension => extension.ExtensionUniqueId.Equals(extensionUniqueId, StringComparison.Ordinal));
        return extension.FirstOrDefault();
    }

    public void EnableExtension(string extensionUniqueId)
    {
        var extension = _installedExtensions.Where(extension => extension.ExtensionUniqueId.Equals(extensionUniqueId, StringComparison.Ordinal));
        _enabledExtensions.Add(extension.First());
    }

    public void DisableExtension(string extensionUniqueId)
    {
        var extension = _enabledExtensions.Where(extension => extension.ExtensionUniqueId.Equals(extensionUniqueId, StringComparison.Ordinal));
        _enabledExtensions.Remove(extension.First());
    }

    private static async Task<IEnumerable<IExtensionWrapper>> GetInstalledExtensionsAsyncUnderLock(bool includeDisabledExtensions, bool refresh)
    {
        if (refresh)
        {
            await RebuildInstalledExtensionsCacheAsync();
        }
        else if (_installedExtensions.Count == 0)
        {
            var extensions = await GetInstalledAppExtensionsAsync();
            foreach (var extension in extensions)
            {
                try
                {
                    var wrappers = await CreateWrappersForExtension(extension);
                    UpdateExtensionsListsFromWrappers(wrappers);
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Failed to load extension '{extension.DisplayName}': {ex.Message}", ex);
                }
            }
        }

        return includeDisabledExtensions ? _installedExtensions : _enabledExtensions;
    }

    private static async Task RebuildInstalledExtensionsCacheAsync()
    {
        var previouslyEnabledExtensionIds = new HashSet<string>(
            _enabledExtensions.Select(static extension => extension.ExtensionUniqueId),
            StringComparer.Ordinal);
        var previouslyInstalledExtensionIds = new HashSet<string>(
            _installedExtensions.Select(static extension => extension.ExtensionUniqueId),
            StringComparer.Ordinal);

        var extensions = await GetInstalledAppExtensionsAsync();
        List<ExtensionWrapper> refreshedWrappers = [];
        foreach (var extension in extensions)
        {
            var wrappers = await CreateWrappersForExtension(extension);
            refreshedWrappers.AddRange(wrappers);
        }

        _installedExtensions.Clear();
        _enabledExtensions.Clear();

        foreach (var extensionWrapper in refreshedWrappers)
        {
            _installedExtensions.Add(extensionWrapper);

            var wasPreviouslyInstalled = previouslyInstalledExtensionIds.Contains(extensionWrapper.ExtensionUniqueId);
            var shouldBeEnabled = !wasPreviouslyInstalled || previouslyEnabledExtensionIds.Contains(extensionWrapper.ExtensionUniqueId);
            if (shouldBeEnabled)
            {
                _enabledExtensions.Add(extensionWrapper);
            }
        }
    }

    private static async Task<IEnumerable<AppExtension>> GetInstalledAppExtensionsAsync() => await AppExtensionCatalog.Open("com.microsoft.commandpalette").FindAllAsync();

    private static async Task<List<ExtensionWrapper>> CreateWrappersForExtension(AppExtension extension)
    {
        var (cmdPalProvider, classIds) = await GetCmdPalExtensionPropertiesAsync(extension);

        if (cmdPalProvider is null || classIds.Count == 0)
        {
            return [];
        }

        List<ExtensionWrapper> wrappers = [];
        foreach (var classId in classIds)
        {
            try
            {
                var extensionWrapper = CreateExtensionWrapper(extension, cmdPalProvider, classId);
                wrappers.Add(extensionWrapper);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to create wrapper for extension '{extension.DisplayName}' classId '{classId}': {ex.Message}");
            }
        }

        return wrappers;
    }

    private static ExtensionWrapper CreateExtensionWrapper(AppExtension extension, IPropertySet cmdPalProvider, string classId)
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
                    CommandPaletteHost.Instance.DebugLog($"Extension {extension.DisplayName} declared an unsupported interface: {supportedInterface.Key}");
                }
            }
        }

        return extensionWrapper;
    }

    private static void UpdateExtensionsListsFromWrappers(List<ExtensionWrapper> wrappers)
    {
        foreach (var extensionWrapper in wrappers)
        {
            _installedExtensions.Add(extensionWrapper);
            _enabledExtensions.Add(extensionWrapper);
        }
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

        classIds.AddRange(GetCreateInstanceList(activation));

        return (cmdPalProvider, classIds);
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

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (!_disposedValue)
        {
            if (disposing)
            {
                _getInstalledExtensionsLock.Dispose();
            }

            _disposedValue = true;
        }
    }
}

internal record struct IsExtensionResult(bool IsExtension, AppExtension? Extension)
{
}
