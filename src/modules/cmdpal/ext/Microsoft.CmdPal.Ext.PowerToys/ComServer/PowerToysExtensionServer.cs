// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using ManagedCommon;
using Microsoft.CommandPalette.Extensions;
using WinRT;

namespace Microsoft.CmdPal.Ext.PowerToys.ComServer;

/// <summary>
/// Local copy of ExtensionServer that accepts IID_IExtension and related IIDs when CmdPal calls CoCreateInstance.
/// </summary>
internal sealed partial class PowerToysExtensionServer : IDisposable
{
    private readonly HashSet<int> _registrationCookies = [];
    private PowerToysExtensionInstanceManager? _instanceManager;
    private ComWrappers? _comWrappers;

    public void RegisterExtension<T>(Func<T> createExtension)
        where T : IExtension
    {
        var exePath = Process.GetCurrentProcess().MainModule?.FileName ?? "unknown";
        Logger.LogInfo($"PowerToys COM server registering CLSID {typeof(T).GUID:B} from {exePath}");

        Trace.WriteLine("Registering PowerToys extension class object:");
        Trace.Indent();
        Trace.WriteLine($"CLSID: {typeof(T).GUID:B}");
        Trace.WriteLine($"Type: {typeof(T)}");

        var clsid = typeof(T).GUID;
        var wrappedCallback = () => (IExtension)createExtension();
        _instanceManager ??= new PowerToysExtensionInstanceManager(wrappedCallback, clsid);
        _comWrappers ??= new StrategyBasedComWrappers();

        var classObjectPtr = _comWrappers.GetOrCreateComInterfaceForObject(_instanceManager, CreateComInterfaceFlags.None);
        var hr = Ole32.CoRegisterClassObject(
            ref clsid,
            classObjectPtr,
            Ole32.CLSCTX_LOCAL_SERVER,
            Ole32.REGCLS_MULTIPLEUSE | Ole32.REGCLS_SUSPENDED,
            out var cookie);

        if (hr < 0)
        {
            Marshal.ThrowExceptionForHR(hr);
        }

        _registrationCookies.Add(cookie);
        Logger.LogInfo($"Registered CLSID {clsid:B} with cookie {cookie}");
        Trace.WriteLine($"Cookie: {cookie}");
        Trace.Unindent();

        hr = Ole32.CoResumeClassObjects();
        if (hr < 0)
        {
            Marshal.ThrowExceptionForHR(hr);
        }

        Logger.LogInfo("CoResumeClassObjects succeeded for PowerToys extension server.");
    }

    public void Dispose()
    {
        Trace.WriteLine("Revoking PowerToys extension class object registrations:");
        Trace.Indent();
        foreach (var cookie in _registrationCookies)
        {
            Trace.WriteLine($"Cookie: {cookie}");
            var hr = Ole32.CoRevokeClassObject(cookie);
            Debug.Assert(hr >= 0, $"CoRevokeClassObject failed ({hr:x}). Cookie: {cookie}");
            Logger.LogInfo($"Revoked PowerToys extension class object cookie {cookie} (hr={hr}).");
        }

        Trace.Unindent();
        Logger.LogInfo("PowerToys extension server dispose completed.");
    }

    private sealed class Ole32
    {
#pragma warning disable SA1310 // Field names should not contain underscore
        public const int CLSCTX_LOCAL_SERVER = 0x4;
        public const int REGCLS_MULTIPLEUSE = 1;
        public const int REGCLS_SUSPENDED = 4;
#pragma warning restore SA1310 // Field names should not contain underscore

        [DllImport(nameof(Ole32))]
        public static extern int CoRegisterClassObject(ref Guid guid, IntPtr obj, int context, int flags, out int register);

        [DllImport(nameof(Ole32))]
        public static extern int CoResumeClassObjects();

        [DllImport(nameof(Ole32))]
        public static extern int CoRevokeClassObject(int register);
    }
}

[ComVisible(true)]
[GeneratedComClass]
#pragma warning disable SA1402 // File may only contain a single type
internal sealed partial class PowerToysExtensionInstanceManager : IClassFactory
#pragma warning restore SA1402 // File may only contain a single type
{
#pragma warning disable SA1310 // Field names should not contain underscore
    private const int E_NOINTERFACE = unchecked((int)0x80004002);
    private const int CLASS_E_NOAGGREGATION = unchecked((int)0x80040110);
    private static readonly Guid IID_IUnknown = Guid.Parse("00000000-0000-0000-C000-000000000046");
    private static readonly Guid IID_IExtension = typeof(IExtension).GUID;
    private static readonly Guid IID_IInspectable = Guid.Parse("AF86E2E0-B12D-4C6A-9C5A-D7AA65101E90");
    private static readonly Guid IID_IAgileObject = Guid.Parse("94EA2B94-E9CC-49E0-C0FF-EE64CA8F5B90");
#pragma warning restore SA1310 // Field names should not contain underscore

    private readonly Func<IExtension> _createExtension;
    private readonly Guid _clsid;

    public PowerToysExtensionInstanceManager(Func<IExtension> createExtension, Guid clsid)
    {
        _createExtension = createExtension;
        _clsid = clsid;
        Logger.LogInfo($"PowerToysExtensionInstanceManager created. IID_IExtension={IID_IExtension:B}, CLSID={_clsid:B}");
    }

    public void CreateInstance(
        [MarshalAs(UnmanagedType.Interface)] object pUnkOuter,
        ref Guid riid,
        out IntPtr ppvObject)
    {
        var requestedIid = riid;
        Logger.LogInfo($"PowerToysExtensionInstanceManager.CreateInstance requested. riid={requestedIid:B}, clsid={_clsid:B}, process={Process.GetCurrentProcess().MainModule?.FileName ?? "unknown"}");

        ppvObject = IntPtr.Zero;

        try
        {
            if (pUnkOuter is not null)
            {
                Logger.LogWarning("Aggregation requested but not supported; returning CLASS_E_NOAGGREGATION.");
                Marshal.ThrowExceptionForHR(CLASS_E_NOAGGREGATION);
            }

            if (requestedIid == _clsid || requestedIid == IID_IUnknown || requestedIid == IID_IExtension || requestedIid == IID_IInspectable || requestedIid == IID_IAgileObject)
            {
                var managed = _createExtension();
                Logger.LogInfo("Managed PowerToysExtension instance created; marshalling to inspectable.");
                var inspectable = MarshalInspectable<IExtension>.FromManaged(managed);
                Logger.LogInfo($"MarshalInspectable returned {(inspectable == IntPtr.Zero ? "null" : inspectable.ToString(CultureInfo.InvariantCulture))}.");
                ppvObject = inspectable;
                Logger.LogInfo("PowerToys extension COM instance created successfully.");
            }
            else
            {
                Logger.LogWarning($"PowerToys extension requested unsupported IID {requestedIid:B}. Throwing E_NOINTERFACE.");
                Marshal.ThrowExceptionForHR(E_NOINTERFACE);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError($"CreateInstance failed for IID {requestedIid:B}, CLSID {_clsid:B}", ex);
            ppvObject = IntPtr.Zero;
            Marshal.ThrowExceptionForHR(E_NOINTERFACE);
        }
    }

    public void LockServer([MarshalAs(UnmanagedType.Bool)] bool fLock)
    {
    }
}

[GeneratedComInterface]
[Guid("00000001-0000-0000-C000-000000000046")]
internal partial interface IClassFactory
{
    void CreateInstance(
        [MarshalAs(UnmanagedType.Interface)] object pUnkOuter,
        ref Guid riid,
        out IntPtr ppvObject);

    void LockServer([MarshalAs(UnmanagedType.Bool)] bool fLock);
}
