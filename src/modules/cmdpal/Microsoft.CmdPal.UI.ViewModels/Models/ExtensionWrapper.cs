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

    // COM/WinRT HRESULT constants used during extension activation
    private const int HResultNoInterface = unchecked((int)0x80004002);   // E_NOINTERFACE
    private const int HResultPathNotFound = unchecked((int)0x80070003);  // E_PATH_NOT_FOUND (HRESULT_FROM_WIN32(ERROR_PATH_NOT_FOUND))

    // IID_IUnknown - the base COM interface always accepted by any well-formed class factory
    private static readonly Guid IID_IUnknown = new("00000000-0000-0000-C000-000000000046");

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

                    unsafe
                    {
                        var extensionPtr = (void*)nint.Zero;
                        try
                        {
                            // First attempt: request IExtension directly.
                            // On Windows 11 23H2 (Build 22631), CoCreateInstance with a custom
                            // WinRT interface IID (like IExtension) may return E_NOINTERFACE because
                            // the OS cannot marshal the interface cross-process without a registered
                            // proxy/stub. Newer Windows builds (24H2+) handle this automatically via
                            // WinRT metadata. In that case, we fall back to IID_IUnknown so the
                            // server can return an IInspectable CCW, which is always marshalable.
                            var extensionIid = typeof(IExtension).GUID;

                            var hr = PInvoke.CoCreateInstance(Guid.Parse(ExtensionClassId), null, CLSCTX.CLSCTX_LOCAL_SERVER, extensionIid, out extensionPtr);

                            var usedIUnknownFallback = false;
                            if (hr.Value == HResultNoInterface)
                            {
                                // On Windows 23H2, the OS may be unable to marshal the IExtension
                                // WinRT interface cross-process. Retry with IID_IUnknown so the
                                // server can return an IInspectable CCW (marshalable on all Windows
                                // versions). We then QI for IExtension from the returned pointer.
                                Logger.LogWarning($"CoCreateInstance for {ExtensionDisplayName} returned E_NOINTERFACE for IID_IExtension (hr=0x{hr.Value:X8}). " +
                                    $"Retrying with IID_IUnknown as a Windows 23H2 compatibility fallback.");

                                extensionPtr = (void*)nint.Zero;
                                hr = PInvoke.CoCreateInstance(Guid.Parse(ExtensionClassId), null, CLSCTX.CLSCTX_LOCAL_SERVER, IID_IUnknown, out extensionPtr);
                                usedIUnknownFallback = true;
                            }

                            if (hr.Value == HResultPathNotFound)
                            {
                                Logger.LogError($"Failed to find {ExtensionDisplayName}: {hr}. It may have been uninstalled or deleted.");
                                return;
                            }
                            else if (hr.Value != 0)
                            {
                                // All other failures — log and bail out. Do NOT fall through to
                                // MarshalInterface below, which would dereference a null pointer and
                                // cause an access violation.
                                Logger.LogError($"Failed to activate {ExtensionDisplayName}: hr=0x{hr.Value:X8}. " +
                                    $"On Windows 23H2 this may indicate that WinRT cross-process interface " +
                                    $"marshaling is unsupported for custom interfaces without a registered proxy/stub.");
                                return;
                            }

                            if (usedIUnknownFallback)
                            {
                                // extensionPtr is an IUnknown/IInspectable cross-process proxy.
                                // Wrap it as IInspectable and then try to QI for IExtension.
                                // On Windows 23H2, this QI may fail (no proxy/stub for IExtension),
                                // resulting in a null _extensionObject. On newer Windows the QI
                                // should succeed via WinRT metadata-based marshaling.
                                var inspectable = MarshalInspectable<object>.FromAbi((nint)extensionPtr);
                                _extensionObject = inspectable as IExtension;
                                if (_extensionObject == null)
                                {
                                    Logger.LogError($"Extension {ExtensionDisplayName} does not expose IExtension across the COM process boundary. " +
                                        $"On Windows 23H2 (Build 22631) and earlier, custom WinRT interfaces cannot be marshaled " +
                                        $"cross-process without a registered proxy/stub. The extension will not be available.");
                                }
                            }
                            else
                            {
                                _extensionObject = MarshalInterface<IExtension>.FromAbi((nint)extensionPtr);
                            }
                        }
                        catch (Exception e)
                        {
                            Logger.LogDebug($"Failed to start {ExtensionDisplayName}. ex: {e.Message}");
                        }
                        finally
                        {
                            if ((nint)extensionPtr != nint.Zero)
                            {
                                Marshal.Release((nint)extensionPtr);
                            }
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
