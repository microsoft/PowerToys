// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using WinRT;

namespace Microsoft.CmdPal.Extensions;

[ComVisible(true)]
internal sealed class ExtensionInstanceManager<T> : IClassFactory
    where T : IExtension
{
#pragma warning disable SA1310 // Field names should not contain underscore

    private const int E_NOINTERFACE = unchecked((int)0x80004002);

    private const int CLASS_E_NOAGGREGATION = unchecked((int)0x80040110);

    private const int E_ACCESSDENIED = unchecked((int)0x80070005);

    // Known constant ignored by win32metadata and cswin32 projections.
    // https://github.com/microsoft/win32metadata/blob/main/generation/WinSDK/RecompiledIdlHeaders/um/processthreadsapi.h
    private static HANDLE CURRENT_THREAD_PSEUDO_HANDLE = (HANDLE)(IntPtr)(-6);

    private static readonly Guid IID_IUnknown = Guid.Parse("00000000-0000-0000-C000-000000000046");

#pragma warning restore SA1310 // Field names should not contain underscore

    private readonly Func<T> _createExtension;

    private readonly bool _restrictToMicrosoftExtensionHosts;

    public ExtensionInstanceManager(Func<T> createExtension, bool restrictToMicrosoftExtensionHosts)
    {
        _createExtension = createExtension;
        _restrictToMicrosoftExtensionHosts = restrictToMicrosoftExtensionHosts;
    }

    public void CreateInstance(
        [MarshalAs(UnmanagedType.Interface)] object pUnkOuter,
        ref Guid riid,
        out IntPtr ppvObject)
    {
        if (_restrictToMicrosoftExtensionHosts && !IsMicrosoftExtensionHost())
        {
            Marshal.ThrowExceptionForHR(E_ACCESSDENIED);
        }

        ppvObject = IntPtr.Zero;

        if (pUnkOuter != null)
        {
            Marshal.ThrowExceptionForHR(CLASS_E_NOAGGREGATION);
        }

        if (riid == typeof(T).GUID || riid == IID_IUnknown)
        {
            // Create the instance of the .NET object
            ppvObject = MarshalInspectable<object>.FromManaged(_createExtension());
        }
        else
        {
            // The object that ppvObject points to does not support the
            // interface identified by riid.
            Marshal.ThrowExceptionForHR(E_NOINTERFACE);
        }
    }

    public void LockServer([MarshalAs(UnmanagedType.Bool)] bool fLock)
    {
    }

    private unsafe bool IsMicrosoftExtensionHost()
    {
        if (PInvoke.CoImpersonateClient() != 0)
        {
            return false;
        }

        uint buffer = 0;
        if (PInvoke.GetPackageFamilyNameFromToken(CURRENT_THREAD_PSEUDO_HANDLE, &buffer, null) != WIN32_ERROR.ERROR_INSUFFICIENT_BUFFER)
        {
            return false;
        }

        var value = new char[buffer];
        fixed (char* p = value)
        {
            if (PInvoke.GetPackageFamilyNameFromToken(CURRENT_THREAD_PSEUDO_HANDLE, &buffer, p) != 0)
            {
                return false;
            }
        }

        if (PInvoke.CoRevertToSelf() != 0)
        {
            return false;
        }

        var valueStr = new string(value);
        switch (valueStr)
        {
            case "Microsoft.Windows.CmdPal_8wekyb3d8bbwe\0":
            case "Microsoft.Windows.CmdPal.Canary_8wekyb3d8bbwe\0":
            case "Microsoft.Windows.CmdPal.Dev_8wekyb3d8bbwe\0":
            case "Microsoft.Windows.DevHome_8wekyb3d8bbwe\0":
            case "Microsoft.Windows.DevHome.Canary_8wekyb3d8bbwe\0":
            case "Microsoft.Windows.DevHome.Dev_8wekyb3d8bbwe\0":
            case "Microsoft.WindowsTerminal\0":
            case "Microsoft.WindowsTerminal_8wekyb3d8bbwe\0":
            case "WindowsTerminalDev_8wekyb3d8bbwe\0":
            case "Microsoft.WindowsTerminalPreview_8wekyb3d8bbwe\0":
                return true;
            default:
                return false;
        }
    }
}

// https://docs.microsoft.com/windows/win32/api/unknwn/nn-unknwn-iclassfactory
[ComImport]
[ComVisible(false)]
[Guid("00000001-0000-0000-C000-000000000046")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IClassFactory
{
    void CreateInstance(
        [MarshalAs(UnmanagedType.Interface)] object pUnkOuter,
        ref Guid riid,
        out IntPtr ppvObject);

    void LockServer([MarshalAs(UnmanagedType.Bool)] bool fLock);
}
