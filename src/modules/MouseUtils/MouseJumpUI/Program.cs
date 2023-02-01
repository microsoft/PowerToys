// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows.Forms;
using MouseJumpUI.Interop;

namespace MouseJumpUI;

internal static class Program
{
    /// <summary>
    ///  The main entry point for the application.
    /// </summary>
    [STAThread]
    private static void Main()
    {
        Program.ConfigureDpiAwareness();

        // To customize application configuration such as set high DPI settings or default font,
        // see https://aka.ms/applicationconfiguration.
        ApplicationConfiguration.Initialize();

        Application.Run(new MainForm());
    }

    private static void ConfigureDpiAwareness()
    {
        // get the current dpi awareness level
        var process = Process.GetCurrentProcess();
        var hResult = Shcore.GetProcessDpiAwareness(
            hProcess: process.Handle,
            value: out var currentDpiAwareness);
        if (hResult != Shcore.S_OK)
        {
            throw new InvalidOperationException(
                $"{nameof(Shcore.GetProcessDpiAwareness)} failed with result {hResult}",
                new Win32Exception(hResult));
        }

        // check we've got the right dpi awareness level set
        var desiredDpiAwareness = Shcore.PROCESS_DPI_AWARENESS.PROCESS_PER_MONITOR_DPI_AWARE;
        if (currentDpiAwareness != desiredDpiAwareness)
        {
            // try to set the current process's dpi awareness level.
            hResult = Shcore.SetProcessDpiAwareness(desiredDpiAwareness);
            if (hResult != Shcore.S_OK)
            {
                throw new InvalidOperationException(
                    $"{nameof(Shcore.SetProcessDpiAwareness)} failed with result {hResult}",
                    new Win32Exception(hResult));
            }
        }
    }
}
