// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using CmdPal.Models;
using Microsoft.CmdPal.Common.Contracts;
using Microsoft.CmdPal.Common.Extensions;
using Microsoft.CmdPal.Common.Services;
using Microsoft.UI.Xaml;
using Microsoft.Windows.CommandPalette.Extensions;
using Windows.ApplicationModel;
using Windows.ApplicationModel.AppExtensions;
using Windows.Foundation.Collections;

namespace Microsoft.Windows.CommandPalette.Services;

public class ExtensionService : IExtensionService, IDisposable
{
    // private readonly ILogger _log = Log.ForContext("SourceContext", nameof(ExtensionService));

    public event EventHandler OnExtensionsChanged = (_, _) => { };

    private static readonly PackageCatalog _catalog = PackageCatalog.OpenForCurrentUser();
    private static readonly object _lock = new();
    private readonly SemaphoreSlim _getInstalledExtensionsLock = new(1, 1);
    private readonly SemaphoreSlim _getInstalledWidgetsLock = new(1, 1);

    private readonly ILocalSettingsService _localSettingsService;

    private bool _disposedValue;

    private const string CreateInstanceProperty = "CreateInstance";
    private const string ClassIdProperty = "@ClassId";

    private static readonly List<IExtensionWrapper> _installedExtensions = new();
    private static readonly List<IExtensionWrapper> _enabledExtensions = new();

    public ExtensionService(ILocalSettingsService settingsService)
    {
        _catalog.PackageInstalling += Catalog_PackageInstalling;
        _catalog.PackageUninstalling += Catalog_PackageUninstalling;
        _catalog.PackageUpdating += Catalog_PackageUpdating;

        // These two were an investigation into getting updates when a package
        // gets redeployed from VS. Neither get raised (nor do the above)
        // _catalog.PackageStatusChanged += Catalog_PackageStatusChanged;
        // _catalog.PackageStaging += Catalog_PackageStaging;
        _localSettingsService = settingsService;
    }

    private void Catalog_PackageInstalling(PackageCatalog sender, PackageInstallingEventArgs args)
    {
        if (args.IsComplete)
        {
            lock (_lock)
            {
                var isCmdPalExtension = Task.Run(() =>
                {
                    return IsValidCmdPalExtension(args.Package);
                }).Result;

                if (isCmdPalExtension)
                {
                    OnPackageChange(args.Package);
                }
            }
        }
    }

    private void Catalog_PackageUninstalling(PackageCatalog sender, PackageUninstallingEventArgs args)
    {
        if (args.IsComplete)
        {
            lock (_lock)
            {
                foreach (var extension in _installedExtensions)
                {
                    if (extension.PackageFullName == args.Package.Id.FullName)
                    {
                        OnPackageChange(args.Package);
                        break;
                    }
                }
            }
        }
    }

    private void Catalog_PackageUpdating(PackageCatalog sender, PackageUpdatingEventArgs args)
    {
        if (args.IsComplete)
        {
            lock (_lock)
            {
                var isCmdPalExtension = Task.Run(() =>
                {
                    return IsValidCmdPalExtension(args.TargetPackage);
                }).Result;

                if (isCmdPalExtension)
                {
                    OnPackageChange(args.TargetPackage);
                }
            }
        }
    }

    private void OnPackageChange(Package package)
    {
        _installedExtensions.Clear();
        _enabledExtensions.Clear();
        OnExtensionsChanged.Invoke(this, EventArgs.Empty);
    }

    private static async Task<bool> IsValidCmdPalExtension(Package package)
    {
        var extensions = await AppExtensionCatalog.Open("com.microsoft.windows.commandpalette").FindAllAsync();
        foreach (var extension in extensions)
        {
            if (package.Id?.FullName == extension.Package?.Id?.FullName)
            {
                var (CmdPalProvider, classId) = await GetCmdPalExtensionPropertiesAsync(extension);
                return CmdPalProvider != null && classId.Count != 0;
            }
        }

        return false;
    }

    private static async Task<(IPropertySet?, List<string>)> GetCmdPalExtensionPropertiesAsync(AppExtension extension)
    {
        var classIds = new List<string>();
        var properties = await extension.GetExtensionPropertiesAsync();

        if (properties is null)
        {
            return (null, classIds);
        }

        var CmdPalProvider = GetSubPropertySet(properties, "CmdPalProvider");
        if (CmdPalProvider is null)
        {
            return (null, classIds);
        }

        var activation = GetSubPropertySet(CmdPalProvider, "Activation");
        if (activation is null)
        {
            return (CmdPalProvider, classIds);
        }

        // Handle case where extension creates multiple instances.
        classIds.AddRange(GetCreateInstanceList(activation));

        return (CmdPalProvider, classIds);
    }

    private static async Task<IEnumerable<AppExtension>> GetInstalledAppExtensionsAsync()
    {
        return await AppExtensionCatalog.Open("com.microsoft.windows.commandpalette").FindAllAsync();
    }

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
                    var (CmdPalProvider, classIds) = await GetCmdPalExtensionPropertiesAsync(extension);
                    if (CmdPalProvider == null || classIds.Count == 0)
                    {
                        continue;
                    }

                    foreach (var classId in classIds)
                    {
                        var extensionWrapper = new ExtensionWrapper(extension, classId);

                        var supportedInterfaces = GetSubPropertySet(CmdPalProvider, "SupportedInterfaces");
                        if (supportedInterfaces is not null)
                        {
                            foreach (var supportedInterface in supportedInterfaces)
                            {
                                ProviderType pt;
                                if (Enum.TryParse<ProviderType>(supportedInterface.Key, out pt))
                                {
                                    extensionWrapper.AddProviderType(pt);
                                }
                                else
                                {
                                    // TODO: throw warning or fire notification that extension declared unsupported extension interface
                                    // https://github.com/microsoft/DevHome/issues/617
                                }
                            }
                        }

                        var localSettingsService = Application.Current.GetService<ILocalSettingsService>();
                        var extensionUniqueId = extension.AppInfo.AppUserModelId + "!" + extension.Id;
                        var isExtensionDisabled = await localSettingsService.ReadSettingAsync<bool>(extensionUniqueId + "-ExtensionDisabled");

                        _installedExtensions.Add(extensionWrapper);
                        if (!isExtensionDisabled)
                        {
                            _enabledExtensions.Add(extensionWrapper);
                        }

                        //TelemetryFactory.Get<ITelemetry>().Log(
                        //    "Extension_ReportInstalled",
                        //    LogLevel.Critical,
                        //    new ReportInstalledExtensionEvent(extensionUniqueId, isEnabled: !isExtensionDisabled));
                    }
                }
            }

            return includeDisabledExtensions ? _installedExtensions : _enabledExtensions;
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

        List<IExtensionWrapper> filteredExtensions = new();
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

    private static IPropertySet? GetSubPropertySet(IPropertySet propSet, string name)
    {
        return propSet.TryGetValue(name, out var value) ? value as IPropertySet : null;
    }

    private static object[]? GetSubPropertySetArray(IPropertySet propSet, string name)
    {
        return propSet.TryGetValue(name, out var value) ? value as object[] : null;
    }

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

    private static string? GetProperty(IPropertySet propSet, string name)
    {
        return propSet[name] as string;
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
    //}
}
