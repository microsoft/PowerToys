// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;

namespace Microsoft.CmdPal.Common.Helpers;

internal static partial class ShellNativeMethods
{
    internal const uint CLSCTX_INPROC_SERVER = 0x1;

    internal static readonly Guid CLSID_WICImagingFactory = new(
        0x317d06e8,
        0x5f24,
        0x433d,
        0xbd,
        0xf7,
        0x79,
        0xce,
        0x68,
        0xd8,
        0xab,
        0xc2);

    [LibraryImport("shell32.dll", StringMarshalling = StringMarshalling.Utf16, SetLastError = false)]
    internal static partial int SHDefExtractIconW(
        string pszIconFile,
        int iIndex,
        uint uFlags,
        out nint phiconLarge,
        out nint phiconSmall,
        int nIconSize);

    [LibraryImport("shell32.dll", StringMarshalling = StringMarshalling.Utf16, SetLastError = false)]
    internal static unsafe partial int SHDefExtractIconW(
        string pszIconFile,
        int iIndex,
        uint uFlags,
        nint* phiconLarge,
        nint* phiconSmall,
        int nIconSize);

    [LibraryImport("ole32.dll", SetLastError = false)]
    private static unsafe partial int CoCreateInstance(
        Guid* rclsid,
        nint pUnkOuter,
        uint dwClsContext,
        Guid* riid,
        void** ppv);

    internal static unsafe global::Windows.Win32.Foundation.HRESULT CoCreateInstance<T>(Guid clsid, out T* ppv)
        where T : unmanaged
    {
        ppv = null;
        var clsidLocal = clsid;
        var iidLocal = typeof(T).GUID;
        void* result = null;
        var hr = (global::Windows.Win32.Foundation.HRESULT)CoCreateInstance(&clsidLocal, 0, CLSCTX_INPROC_SERVER, &iidLocal, &result);
        ppv = (T*)result;
        return hr;
    }
}
