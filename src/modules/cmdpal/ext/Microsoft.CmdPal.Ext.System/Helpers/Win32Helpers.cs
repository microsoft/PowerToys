// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;

namespace Microsoft.CmdPal.Ext.System.Helpers;

public static class Win32Helpers
{
    /// <summary>
    /// Detects the type of system firmware which is equal to the boot type by calling the method <see cref="NativeMethods.GetFirmwareType"/>.
    /// </summary>
    /// <returns>Firmware type like Uefi or Bios.</returns>
    public static FirmwareType GetSystemFirmwareType()
    {
        FirmwareType firmwareType = default;
        _ = NativeMethods.GetFirmwareType(ref firmwareType);
        return firmwareType;
    }

    /// <summary>
    /// Returns the last Win32 Error code thrown by a native method if enabled for this method.
    /// </summary>
    /// <returns>The error code as int value.</returns>
    public static int GetLastError()
    {
        return Marshal.GetLastPInvokeError();
    }

    /// <summary>
    /// Validate that the handle is not null and close it.
    /// </summary>
    /// <param name="handle">Handle to close.</param>
    /// <returns>Zero if native method fails and nonzero if the native method succeeds.</returns>
    public static bool CloseHandleIfNotNull(IntPtr handle)
    {
        if (handle == IntPtr.Zero)
        {
            // Return true if there is nothing to close.
            return true;
        }

        return NativeMethods.CloseHandle(handle);
    }

    /// <summary>
    /// Gets the description for an HRESULT error code.
    /// </summary>
    /// <param name="hr">The HRESULT number</param>
    /// <returns>A string containing the description.</returns>
    public static string MessageFromHResult(int hr)
    {
        return Marshal.GetExceptionForHR(hr)?.Message ?? string.Empty;
    }
}
