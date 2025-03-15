// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Common.Services;
using Microsoft.CommandPalette.Extensions;
using Windows.ApplicationModel;
using Windows.ApplicationModel.AppExtensions;
using Windows.Foundation;
using Windows.Foundation.Collections;

namespace Microsoft.CmdPal.UI.ViewModels.Models;

public class ExtensionService : IExtensionService, IDisposable
{
    public event TypedEventHandler<IExtensionService, IEnumerable<IExtensionWrapper>>? OnExtensionAdded;

    public event TypedEventHandler<IExtensionService, IEnumerable<IExtensionWrapper>>? OnExtensionRemoved;

    private static readonly PackageCatalog _catalog = PackageCatalog.OpenForCurrentUser();
    private static readonly Lock _lock = new();
    private readonly SemaphoreSlim _getInstalledExtensionsLock = new(1, 1);
    private readonly SemaphoreSlim _getInstalledWidgetsLock = new(1, 1);

    // private readonly ILocalSettingsService _localSettingsService;
    private bool _disposedValue;

    private const string CreateInstanceProperty = "CreateInstance";
    private const string ClassIdProperty = "@ClassId";

    private static readonly List<IExtensionWrapper> _installedExtensions = [];
    private static readonly List<IExtensionWrapper> _enabledExtensions = [];

    public ExtensionService()
    {
        _catalog.PackageInstalling += Catalog_PackageInstalling;
        _catalog.PackageUninstalling += Catalog_PackageUninstalling;
        _catalog.PackageUpdating += Catalog_PackageUpdating;

        //// These two were an investigation into getting updates when a package
        //// gets redeployed from VS. Neither get raised (nor do the above)
        //// _catalog.PackageStatusChanged += Catalog_PackageStatusChanged;
        //// _catalog.PackageStaging += Catalog_PackageStaging;
        // _localSettingsService = settingsService;
    }

    private void Catalog_PackageInstalling(PackageCatalog sender, PackageInstallingEventArgs args)
    {
        if (args.IsComplete)
        {
            lock (_lock)
            {
                InstallPackageUnderLock(args.Package);
            }
        }
    }

    private void Catalog_PackageUninstalling(PackageCatalog sender, PackageUninstallingEventArgs args)
    {
        if (args.IsComplete)
        {
            lock (_lock)
            {
                UninstallPackageUnderLock(args.Package);
            }
        }
    }

    private void Catalog_PackageUpdating(PackageCatalog sender, PackageUpdatingEventArgs args)
    {
        if (args.IsComplete)
        {
            lock (_lock)
            {
                // Get any extension providers that we previously had from this app
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
        if (isExtension && extension != null)
        {
            CommandPaletteHost.Instance.DebugLog($"Installed new extension app {extension.DisplayName}");

            Task.Run(async () =>
            {
                await _getInstalledExtensionsLock.WaitAsync();
                try
                {
                    var wrappers = await CreateWrappersForExtension(extension);

                    UpdateExtensionsListsFromWrappers(wrappers);

                    OnExtensionAdded?.Invoke(this, wrappers);
                }
                finally
                {
                    _getInstalledExtensionsLock.Release();
                }
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
                CommandPaletteHost.Instance.DebugLog($"Uninstalled extension app {extension.PackageDisplayName}");

                removedExtensions.Add(extension);
            }
        }

        Task.Run(async () =>
        {
            await _getInstalledExtensionsLock.WaitAsync();
            try
            {
                _installedExtensions.RemoveAll(i => removedExtensions.Contains(i));

                OnExtensionRemoved?.Invoke(this, removedExtensions);
            }
            finally
            {
                _getInstalledExtensionsLock.Release();
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

                return new(cmdPalProvider != null && classId.Count != 0, extension);
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

        // Handle case where extension creates multiple instances.
        classIds.AddRange(GetCreateInstanceList(activation));

        return (cmdPalProvider, classIds);
    }

    private static async Task<IEnumerable<AppExtension>> GetInstalledAppExtensionsAsync() => await AppExtensionCatalog.Open("com.microsoft.commandpalette").FindAllAsync();

    public async Task<IEnumerable<IExtensionWrapper>> GetInstalledExtensionsAsync(bool includeDisabledExtensions = false)
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
                    UpdateExtensionsListsFromWrappers(wrappers);
                }
            }

            return includeDisabledExtensions ? _installedExtensions : _enabledExtensions;
        }
        finally
        {
            _getInstalledExtensionsLock.Release();
        }
    }

    private static void UpdateExtensionsListsFromWrappers(List<ExtensionWrapper> wrappers)
    {
        foreach (var extensionWrapper in wrappers)
        {
            // var localSettingsService = Application.Current.GetService<ILocalSettingsService>();
            var extensionUniqueId = extensionWrapper.ExtensionUniqueId;
            var isExtensionDisabled = false; // await localSettingsService.ReadSettingAsync<bool>(extensionUniqueId + "-ExtensionDisabled");

            _installedExtensions.Add(extensionWrapper);
            if (!isExtensionDisabled)
            {
                _enabledExtensions.Add(extensionWrapper);
            }

            // TelemetryFactory.Get<ITelemetry>().Log(
            //    "Extension_ReportInstalled",
            //    LogLevel.Critical,
            //    new ReportInstalledExtensionEvent(extensionUniqueId, isEnabled: !isExtensionDisabled));
        }
    }

    private static async Task<List<ExtensionWrapper>> CreateWrappersForExtension(AppExtension extension)
    {
        var (cmdPalProvider, classIds) = await GetCmdPalExtensionPropertiesAsync(extension);

        if (cmdPalProvider == null || classIds.Count == 0)
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
                    // log warning  that extension declared unsupported extension interface
                    CommandPaletteHost.Instance.DebugLog($"Extension {extension.DisplayName} declared an unsupported interface: {supportedInterface.Key}");
                }
            }
        }

        return extensionWrapper;
    }

    public IExtensionWrapper? GetInstalledExtension(string extensionUniqueId)
    {
        var extension = _installedExtensions.Where(extension => extension.ExtensionUniqueId.Equals(extensionUniqueId, StringComparison.Ordinal));
        return extension.FirstOrDefault();
    }

    public async Task SignalStopExtensionsAsync()
    {
        var installedExtensions = await GetInstalledExtensionsAsync();
        foreach (var installedExtension in installedExtensions)
        {
            if (installedExtension.IsRunning())
            {
                installedExtension.SignalDispose();
            }
        }
    }

    public async Task<IEnumerable<IExtensionWrapper>> GetInstalledExtensionsAsync(ProviderType providerType, bool includeDisabledExtensions = false)
    {
        var installedExtensions = await GetInstalledExtensionsAsync(includeDisabledExtensions);

        List<IExtensionWrapper> filteredExtensions = [];
        foreach (var installedExtension in installedExtensions)
        {
            if (installedExtension.HasProviderType(providerType))
            {
                filteredExtensions.Add(installedExtension);
            }
        }

        return filteredExtensions;
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

    private static IPropertySet? GetSubPropertySet(IPropertySet propSet, string name) => propSet.TryGetValue(name, out var value) ? value as IPropertySet : null;

    private static object[]? GetSubPropertySetArray(IPropertySet propSet, string name) => propSet.TryGetValue(name, out var value) ? value as object[] : null;

    /// <summary>
    /// There are cases where the extension creates multiple COM instances.
    /// </summary>
    /// <param name="activationPropSet">Activation property set object</param>
    /// <returns>List of ClassId strings associated with the activation property</returns>
    private static List<string> GetCreateInstanceList(IPropertySet activationPropSet)
    {
        var propSetList = new List<string>();
        var singlePropertySet = GetSubPropertySet(activationPropSet, CreateInstanceProperty);
        if (singlePropertySet != null)
        {
            var classId = GetProperty(singlePropertySet, ClassIdProperty);

            // If the instance has a classId as a single string, then it's only supporting a single instance.
            if (classId != null)
            {
                propSetList.Add(classId);
            }
        }
        else
        {
            var propertySetArray = GetSubPropertySetArray(activationPropSet, CreateInstanceProperty);
            if (propertySetArray != null)
            {
                foreach (var prop in propertySetArray)
                {
                    if (prop is not IPropertySet propertySet)
                    {
                        continue;
                    }

                    var classId = GetProperty(propertySet, ClassIdProperty);
                    if (classId != null)
                    {
                        propSetList.Add(classId);
                    }
                }
            }
        }

        return propSetList;
    }

    private static string? GetProperty(IPropertySet propSet, string name) => propSet[name] as string;

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

    /*
    ///// <inheritdoc cref="IExtensionService.DisableExtensionIfWindowsFeatureNotAvailable(IExtensionWrapper)"/>
    //public async Task<bool> DisableExtensionIfWindowsFeatureNotAvailable(IExtensionWrapper extension)
    //{
    //    // Only attempt to disable feature if its available.
    //    if (IsWindowsOptionalFeatureAvailableForExtension(extension.ExtensionClassId))
    //    {
    //        return false;
    //    }
    //    _log.Warning($"Disabling extension: '{extension.ExtensionDisplayName}' because its feature is absent or unknown");
    //    // Remove extension from list of enabled extensions to prevent Dev Home from re-querying for this extension
    //    // for the rest of its process lifetime.
    //    DisableExtension(extension.ExtensionUniqueId);
    //    // Update the local settings so the next time the user launches Dev Home the extension will be disabled.
    //    await _localSettingsService.SaveSettingAsync(extension.ExtensionUniqueId + "-ExtensionDisabled", true);
    //    return true;
    //} */
}

internal record struct IsExtensionResult(bool IsExtension, AppExtension? Extension)
{
}
