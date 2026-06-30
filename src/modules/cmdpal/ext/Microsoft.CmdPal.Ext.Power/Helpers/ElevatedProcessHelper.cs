// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel;
using System.Diagnostics;
using Windows.Win32;
using Windows.Win32.Foundation;

namespace Microsoft.CmdPal.Ext.Power.Helpers;

internal static class ElevatedProcessHelper
{
    internal static bool TryRunElevated(string file, string arguments, out int exitCode, out WIN32_ERROR? win32Error)
    {
        exitCode = -1;
        win32Error = null;

        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = file,
                Arguments = arguments,
                UseShellExecute = true,
                Verb = "runas",
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
            });

            if (process is null)
            {
                return false;
            }

            process.WaitForExit();
            exitCode = process.ExitCode;
            return exitCode == 0;
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == (int)WIN32_ERROR.ERROR_CANCELLED)
        {
            win32Error = WIN32_ERROR.ERROR_CANCELLED;
            return false;
        }
        catch (Win32Exception ex)
        {
            win32Error = (WIN32_ERROR)ex.NativeErrorCode;
            return false;
        }
    }

    internal static bool IsUacCancelled(WIN32_ERROR? win32Error) =>
        win32Error == WIN32_ERROR.ERROR_CANCELLED;
}
