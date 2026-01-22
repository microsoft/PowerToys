// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace Microsoft.CommandPalette.Extensions;

public sealed partial class ExtensionServer : IDisposable
{
    private readonly HashSet<int> registrationCookies = [];
    private ExtensionInstanceManager? _extensionInstanceManager;
    private ComWrappers? _comWrappers;

    public void RegisterExtension<T>(Func<T> createExtension, bool restrictToMicrosoftExtensionHosts = false)
        where T : IExtension
    {
        Trace.WriteLine($"Registering class object:");
        Trace.Indent();
        Trace.WriteLine($"CLSID: {typeof(T).GUID:B}");
        Trace.WriteLine($"Type: {typeof(T)}");

        int cookie;
        var clsid = typeof(T).GUID;
        var wrappedCallback = () => (IExtension)createExtension();
        _extensionInstanceManager ??= new ExtensionInstanceManager(wrappedCallback, restrictToMicrosoftExtensionHosts, typeof(T).GUID);
        _comWrappers ??= new StrategyBasedComWrappers();

        var f = _comWrappers.GetOrCreateComInterfaceForObject(_extensionInstanceManager, CreateComInterfaceFlags.None);

        var hr = Ole32.CoRegisterClassObject(
            ref clsid,
            f,
            Ole32.CLSCTX_LOCAL_SERVER,
            Ole32.REGCLS_MULTIPLEUSE | Ole32.REGCLS_SUSPENDED,
            out cookie);

        if (hr < 0)
        {
            Marshal.ThrowExceptionForHR(hr);
        }

        registrationCookies.Add(cookie);
        Trace.WriteLine($"Cookie: {cookie}");
        Trace.Unindent();

        hr = Ole32.CoResumeClassObjects();
        if (hr < 0)
        {
            Marshal.ThrowExceptionForHR(hr);
        }
    }

#pragma warning disable CA1822 // Mark members as static
    public void Run() =>

        // TODO : We need to handle lifetime management of the server.
        // For details around ref counting and locking of out-of-proc COM servers, see
        // https://docs.microsoft.com/windows/win32/com/out-of-process-server-implementation-helpers
        Console.ReadLine();

    public void Dispose()
    {
        Trace.WriteLine($"Revoking class object registrations:");
        Trace.Indent();
        foreach (var cookie in registrationCookies)
        {
            Trace.WriteLine($"Cookie: {cookie}");
            var hr = Ole32.CoRevokeClassObject(cookie);
            Debug.Assert(hr >= 0, $"CoRevokeClassObject failed ({hr:x}). Cookie: {cookie}");
        }

        Trace.Unindent();
    }

    private sealed class Ole32
    {
#pragma warning disable SA1310 // Field names should not contain underscore
        // https://docs.microsoft.com/windows/win32/api/wtypesbase/ne-wtypesbase-clsctx
        public const int CLSCTX_LOCAL_SERVER = 0x4;

        // https://docs.microsoft.com/windows/win32/api/combaseapi/ne-combaseapi-regcls
        public const int REGCLS_MULTIPLEUSE = 1;
        public const int REGCLS_SUSPENDED = 4;
#pragma warning restore SA1310 // Field names should not contain underscore

        // https://docs.microsoft.com/windows/win32/api/combaseapi/nf-combaseapi-coregisterclassobject
        [DllImport(nameof(Ole32))]
        public static extern int CoRegisterClassObject(ref Guid guid, IntPtr obj, int context, int flags, out int register);

        // https://docs.microsoft.com/windows/win32/api/combaseapi/nf-combaseapi-coresumeclassobjects
        [DllImport(nameof(Ole32))]
        public static extern int CoResumeClassObjects();

        // https://docs.microsoft.com/windows/win32/api/combaseapi/nf-combaseapi-corevokeclassobject
        [DllImport(nameof(Ole32))]
        public static extern int CoRevokeClassObject(int register);
    }
}
