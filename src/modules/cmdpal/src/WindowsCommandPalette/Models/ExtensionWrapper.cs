// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Runtime.InteropServices;
using Microsoft.CmdPal.Common.Services;
using Microsoft.Windows.CommandPalette.Extensions;
using Windows.ApplicationModel;
using Windows.ApplicationModel.AppExtensions;
using Windows.Win32;
using Windows.Win32.System.Com;
using WinRT;

namespace CmdPal.Models;

public class ExtensionWrapper : IExtensionWrapper
{
    private const int HResultRpcServerNotRunning = -2147023174;

    private readonly object _lock = new();
    private readonly List<ProviderType> _providerTypes = new();

    private readonly Dictionary<Type, ProviderType> _providerTypeMap = new()
    {
        [typeof(ICommandProvider)] = ProviderType.Commands
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
        ExtensionUniqueId = appExtension.AppInfo.AppUserModelId + "!" + appExtension.Id;
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
    public string ExtensionUniqueId { get; }

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
                    var extensionPtr = IntPtr.Zero;
                    try
                    {
                        var hr = PInvoke.CoCreateInstance(Guid.Parse(ExtensionClassId), null, CLSCTX.CLSCTX_LOCAL_SERVER, typeof(IExtension).GUID, out var extensionObj);
                        extensionPtr = Marshal.GetIUnknownForObject(extensionObj);
                        if (hr < 0)
                        {
                            Marshal.ThrowExceptionForHR(hr);
                        }

                        _extensionObject = MarshalInterface<IExtension>.FromAbi(extensionPtr);
                    }
                    finally
                    {
                        if (extensionPtr != IntPtr.Zero)
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
            if (IsRunning())
            {
                return _extensionObject;
            }
            else
            {
                return null;
            }
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
            return new List<T>() { singleProviderSupported };
        }

        return Enumerable.Empty<T>();
    }

    public void AddProviderType(ProviderType providerType)
    {
        _providerTypes.Add(providerType);
    }

    public bool HasProviderType(ProviderType providerType)
    {
        return _providerTypes.Contains(providerType);
    }
}
