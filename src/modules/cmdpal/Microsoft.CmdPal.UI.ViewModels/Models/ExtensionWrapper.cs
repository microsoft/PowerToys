// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;
using ManagedCommon;
using Microsoft.CmdPal.Common.Services;
using Microsoft.CommandPalette.Extensions;
using Windows.ApplicationModel;
using Windows.ApplicationModel.AppExtensions;
using Windows.Win32;
using Windows.Win32.System.Com;
using WinRT;

namespace Microsoft.CmdPal.UI.ViewModels.Models;

public class ExtensionWrapper : IExtensionWrapper
{
    private const int HResultRpcServerNotRunning = -2147023174;

    private readonly string _appUserModelId;
    private readonly string _extensionId;

    private readonly Lock _lock = new();
    private readonly List<ProviderType> _providerTypes = [];

    private readonly Dictionary<Type, ProviderType> _providerTypeMap = new()
    {
        [typeof(ICommandProvider)] = ProviderType.Commands,
    };

    private IExtension? _extensionObject;

    public ExtensionWrapper(AppExtension appExtension, string classId)
    {
        PackageDisplayName = appExtension.Package.DisplayName;
        ExtensionDisplayName = appExtension.DisplayName;
        PackageFullName = appExtension.Package.Id.FullName;
        PackageFamilyName = appExtension.Package.Id.FamilyName;
        ExtensionClassId = classId ?? throw new ArgumentNullException(nameof(classId));
        Publisher = appExtension.Package.PublisherDisplayName;
        InstalledDate = appExtension.Package.InstalledDate;
        Version = appExtension.Package.Id.Version;
        _appUserModelId = appExtension.AppInfo.AppUserModelId;
        _extensionId = appExtension.Id;
    }

    public string PackageDisplayName { get; }

    public string ExtensionDisplayName { get; }

    public string PackageFullName { get; }

    public string PackageFamilyName { get; }

    public string ExtensionClassId { get; }

    public string Publisher { get; }

    public DateTimeOffset InstalledDate { get; }

    public PackageVersion Version { get; }

    /// <summary>
    /// Gets the unique id for this Dev Home extension. The unique id is a concatenation of:
    /// <list type="number">
    /// <item>The AppUserModelId (AUMID) of the extension's application. The AUMID is the concatenation of the package
    /// family name and the application id and uniquely identifies the application containing the extension within
    /// the package.</item>
    /// <item>The Extension Id. This is the unique identifier of the extension within the application.</item>
    /// </list>
    /// </summary>
    public string ExtensionUniqueId => _appUserModelId + "!" + _extensionId;

    public bool IsRunning()
    {
        if (_extensionObject is null)
        {
            return false;
        }

        try
        {
            _extensionObject.As<IInspectable>().GetRuntimeClassName();
        }
        catch (COMException e)
        {
            if (e.ErrorCode == HResultRpcServerNotRunning)
            {
                return false;
            }

            throw;
        }

        return true;
    }

    public async Task StartExtensionAsync()
    {
        await Task.Run(() =>
        {
            lock (_lock)
            {
                if (!IsRunning())
                {
                    Logger.LogDebug($"Starting {ExtensionDisplayName} ({ExtensionClassId})");

                    var extensionPtr = nint.Zero;
                    try
                    {
                        // -2147024809: E_INVALIDARG
                        // -2147467262: E_NOINTERFACE
                        // -2147024893: E_PATH_NOT_FOUND
                        var guid = typeof(IExtension).GUID;
                        var hr = PInvoke.CoCreateInstance(Guid.Parse(ExtensionClassId), null, CLSCTX.CLSCTX_LOCAL_SERVER, guid, out var extensionObj);

                        if (hr.Value == -2147024893)
                        {
                            Logger.LogDebug($"Failed to find {ExtensionDisplayName}: {hr}. It may have been uninstalled or deleted.");

                            // We don't really need to throw this exception.
                            // We'll just return out nothing.
                            return;
                        }

                        extensionPtr = Marshal.GetIUnknownForObject(extensionObj);
                        if (hr < 0)
                        {
                            Logger.LogDebug($"Failed to instantiate {ExtensionDisplayName}: {hr}");
                            Marshal.ThrowExceptionForHR(hr);
                        }

                        _extensionObject = MarshalInterface<IExtension>.FromAbi(extensionPtr);
                    }
                    finally
                    {
                        if (extensionPtr != nint.Zero)
                        {
                            Marshal.Release(extensionPtr);
                        }
                    }
                }
            }
        });
    }

    public void SignalDispose()
    {
        lock (_lock)
        {
            if (IsRunning())
            {
                _extensionObject?.Dispose();
            }

            _extensionObject = null;
        }
    }

    public IExtension? GetExtensionObject()
    {
        lock (_lock)
        {
            return IsRunning() ? _extensionObject : null;
        }
    }

    public async Task<T?> GetProviderAsync<T>()
        where T : class
    {
        await StartExtensionAsync();

        return GetExtensionObject()?.GetProvider(_providerTypeMap[typeof(T)]) as T;
    }

    public async Task<IEnumerable<T>> GetListOfProvidersAsync<T>()
        where T : class
    {
        await StartExtensionAsync();

        var supportedProviders = GetExtensionObject()?.GetProvider(_providerTypeMap[typeof(T)]);
        if (supportedProviders is IEnumerable<T> multipleProvidersSupported)
        {
            return multipleProvidersSupported;
        }
        else if (supportedProviders is T singleProviderSupported)
        {
            return [singleProviderSupported];
        }

        return Enumerable.Empty<T>();
    }

    public void AddProviderType(ProviderType providerType) => _providerTypes.Add(providerType);

    public bool HasProviderType(ProviderType providerType) => _providerTypes.Contains(providerType);
}
