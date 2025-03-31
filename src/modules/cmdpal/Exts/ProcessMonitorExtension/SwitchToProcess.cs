// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace ProcessMonitorExtension;

internal sealed partial class SwitchToProcess : InvokableCommand
{
    [DllImport("user32.dll")]
    public static extern void SwitchToThisWindow(IntPtr hWnd, bool fAltTab);

    private readonly ProcessItem process;

    public SwitchToProcess(ProcessItem process)
    {
        this.process = process;
        this.Icon = new IconInfo(process.ExePath == string.Empty ? "\uE7B8" : process.ExePath);
        this.Name = "Switch to";
    }

    public override CommandResult Invoke()
    {
        SwitchToThisWindow(process.Process.MainWindowHandle, true);
        return CommandResult.KeepOpen();
    }
}
