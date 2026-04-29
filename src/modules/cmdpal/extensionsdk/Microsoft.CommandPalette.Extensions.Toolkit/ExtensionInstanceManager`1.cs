// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using Windows.Win32;
using Windows.Win32.Foundation;
using WinRT;

namespace Microsoft.CommandPalette.Extensions;

[ComVisible(true)]
[GeneratedComClass]
internal sealed partial class ExtensionInstanceManager : IClassFactory
{
#pragma warning disable SA1310 // Field names should not contain underscore

    private const int E_NOINTERFACE = unchecked((int)0x80004002);

    private const int CLASS_E_NOAGGREGATION = unchecked((int)0x80040110);

    private const int E_ACCESSDENIED = unchecked((int)0x80070005);

    // Known constant ignored by win32metadata and cswin32 projections.
    // https://github.com/microsoft/win32metadata/blob/main/generation/WinSDK/RecompiledIdlHeaders/um/processthreadsapi.h
    private static readonly HANDLE CURRENT_THREAD_PSEUDO_HANDLE = (HANDLE)(IntPtr)(-6);

    private static readonly Guid IID_IUnknown = Guid.Parse("00000000-0000-0000-C000-000000000046");

    // IInspectable is the WinRT base interface (always marshalable cross-process on all Windows versions).
    private static readonly Guid IID_IInspectable = Guid.Parse("AF86E2E0-B12D-4C6A-9C5A-D7AA65101E90");

    // IExtension's WinRT interface IID. On Windows 11 23H2 (Build 22631), CoCreateInstance may
    // invoke CreateInstance with this IID directly. We handle it by returning the IInspectable CCW,
    // which COM on all Windows versions can marshal cross-process. On 24H2+ the OS uses WinRT
    // metadata (winmd) to marshal custom interfaces transparently.
    private static readonly Guid IID_IExtension = typeof(IExtension).GUID;

#pragma warning restore SA1310 // Field names should not contain underscore

    private readonly Func<IExtension> _createExtension;

    private readonly bool _restrictToMicrosoftExtensionHosts;

    private readonly Guid _clsid;

    public ExtensionInstanceManager(Func<IExtension> createExtension, bool restrictToMicrosoftExtensionHosts, Guid clsid)
    {
        _createExtension = createExtension;
        _restrictToMicrosoftExtensionHosts = restrictToMicrosoftExtensionHosts;
        _clsid = clsid;
    }

    public void CreateInstance(
        [MarshalAs(UnmanagedType.Interface)] object pUnkOuter,
        Guid riid,
        out IntPtr ppvObject)
    {
        if (_restrictToMicrosoftExtensionHosts && !IsMicrosoftExtensionHost())
        {
            Marshal.ThrowExceptionForHR(E_ACCESSDENIED);
        }

        ppvObject = IntPtr.Zero;

        if (pUnkOuter is not null)
        {
            Marshal.ThrowExceptionForHR(CLASS_E_NOAGGREGATION);
        }

        if (riid == _clsid || riid == IID_IUnknown || riid == IID_IInspectable || riid == IID_IExtension)
        {
            // Create the instance of the .NET object and return it as IInspectable.
            // Returning IInspectable ensures the CCW is marshalable cross-process on all
            // Windows versions (including 23H2), since IInspectable has built-in OS support.
            // The client will resolve the specific WinRT interface (IExtension) via CsWinRT.
            var managed = _createExtension();
            var ins = MarshalInspectable<object>.FromManaged(managed);
            ppvObject = ins;
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
        return valueStr switch
        {
            "Microsoft.Windows.CmdPal_8wekyb3d8bbwe\0" or "Microsoft.Windows.CmdPal.Canary_8wekyb3d8bbwe\0" or "Microsoft.Windows.CmdPal.Dev_8wekyb3d8bbwe\0" or "Microsoft.Windows.DevHome_8wekyb3d8bbwe\0" or "Microsoft.Windows.DevHome.Canary_8wekyb3d8bbwe\0" or "Microsoft.Windows.DevHome.Dev_8wekyb3d8bbwe\0" or "Microsoft.WindowsTerminal\0" or "Microsoft.WindowsTerminal_8wekyb3d8bbwe\0" or "WindowsTerminalDev_8wekyb3d8bbwe\0" or "Microsoft.WindowsTerminalPreview_8wekyb3d8bbwe\0" => true,
            _ => false,
        };
    }
}

// https://docs.microsoft.com/windows/win32/api/unknwn/nn-unknwn-iclassfactory
[GeneratedComInterface]
[Guid("00000001-0000-0000-C000-000000000046")]
internal partial interface IClassFactory
{
    void CreateInstance(
        [MarshalAs(UnmanagedType.Interface)] object pUnkOuter,
        Guid riid,
        out IntPtr ppvObject);

    void LockServer([MarshalAs(UnmanagedType.Bool)] bool fLock);
}
