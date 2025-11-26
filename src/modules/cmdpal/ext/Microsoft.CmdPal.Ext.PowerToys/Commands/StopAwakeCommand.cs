// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace PowerToysExtension.Commands;

internal sealed partial class StopAwakeCommand : InvokableCommand
{
    private const string AwakeWindowClass = "Awake.MessageWindow";
    private const uint WmCommand = 0x0111;
    private const int PassiveCommand = 0x0400 + 0x3;

    internal StopAwakeCommand()
    {
        Name = "Set Awake to Off";
    }

    public override CommandResult Invoke()
    {
        try
        {
            if (TrySendPassiveCommand())
            {
                return CommandResult.ShowToast("Awake switched to Off.");
            }

            return CommandResult.ShowToast("Awake does not appear to be running.");
        }
        catch (Exception ex)
        {
            return CommandResult.ShowToast($"Failed to switch Awake off: {ex.Message}");
        }
    }

    private static bool TrySendPassiveCommand()
    {
        var handle = IntPtr.Zero;
        var sent = false;

        while (true)
        {
            handle = FindWindowEx(IntPtr.Zero, handle, AwakeWindowClass, null);
            if (handle == IntPtr.Zero)
            {
                break;
            }

            if (PostMessage(handle, WmCommand, new IntPtr(PassiveCommand), IntPtr.Zero))
            {
                sent = true;
            }
        }

        return sent;
    }

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr FindWindowEx(IntPtr parentHandle, IntPtr childAfter, string? className, string? windowTitle);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
}
