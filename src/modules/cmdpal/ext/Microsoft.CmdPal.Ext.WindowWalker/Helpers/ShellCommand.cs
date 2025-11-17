// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Text;
using System.Threading;

namespace Microsoft.CmdPal.Ext.WindowWalker.Helpers;

public static class ShellCommand
{
    public delegate bool EnumThreadDelegate(IntPtr hwnd, IntPtr lParam);

    private static bool containsSecurityWindow;

    public static Process? RunAsDifferentUser(ProcessStartInfo processStartInfo)
    {
        ArgumentNullException.ThrowIfNull(processStartInfo);

        processStartInfo.Verb = "RunAsUser";
        var process = Process.Start(processStartInfo);

        containsSecurityWindow = false;

        // wait for windows to bring up the "Windows Security" dialog
        while (!containsSecurityWindow)
        {
            CheckSecurityWindow();
            Thread.Sleep(25);
        }

        // while this process contains a "Windows Security" dialog, stay open
        while (containsSecurityWindow)
        {
            containsSecurityWindow = false;
            CheckSecurityWindow();
            Thread.Sleep(50);
        }

        return process;
    }

    private static void CheckSecurityWindow()
    {
        ProcessThreadCollection ptc = Process.GetCurrentProcess().Threads;
        for (var i = 0; i < ptc.Count; i++)
        {
            NativeMethods.EnumThreadWindows((uint)ptc[i].Id, CheckSecurityThread, IntPtr.Zero);
        }
    }

    private static bool CheckSecurityThread(IntPtr hwnd, IntPtr lParam)
    {
        if (GetWindowTitle(hwnd) == "Windows Security")
        {
            containsSecurityWindow = true;
        }

        return true;
    }

    private static string GetWindowTitle(IntPtr hwnd)
    {
        StringBuilder sb = new StringBuilder(NativeMethods.GetWindowTextLength(hwnd) + 1);
        _ = NativeMethods.GetWindowText(hwnd, sb, sb.Capacity);
        return sb.ToString();
    }

    public static ProcessStartInfo SetProcessStartInfo(this string fileName, string workingDirectory = "", string arguments = "", string verb = "")
    {
        var info = new ProcessStartInfo
        {
            FileName = fileName,
            WorkingDirectory = workingDirectory,
            Arguments = arguments,
            Verb = verb,
        };

        return info;
    }
}
