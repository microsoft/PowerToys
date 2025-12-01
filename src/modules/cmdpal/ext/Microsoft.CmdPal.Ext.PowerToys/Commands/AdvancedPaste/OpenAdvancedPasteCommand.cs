// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using ManagedCommon;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace PowerToysExtension.Commands;

/// <summary>
/// Opens Advanced Paste UI, reusing an existing instance when present.
/// </summary>
internal sealed partial class OpenAdvancedPasteCommand : InvokableCommand
{
    public OpenAdvancedPasteCommand()
    {
        Name = "Open Advanced Paste";
    }

    public override CommandResult Invoke()
    {
        try
        {
            if (TryActivateExisting())
            {
                return CommandResult.Dismiss();
            }

            return CommandResult.ShowToast("Advanced Paste is not running. Please start it first.");
        }
        catch (Exception ex)
        {
            return CommandResult.ShowToast($"Failed to open Advanced Paste: {ex.Message}");
        }
    }

    private static bool TryActivateExisting()
    {
        try
        {
            var processes = Process.GetProcessesByName("PowerToys.AdvancedPaste");
            Logger.LogInfo($"AdvancedPaste: found {processes.Length} processes.");

            foreach (var proc in processes)
            {
                if (proc.HasExited)
                {
                    Logger.LogInfo($"AdvancedPaste: pid {proc.Id} already exited.");
                    continue;
                }

                proc.Refresh();
                var handle = proc.MainWindowHandle;

                if (handle != IntPtr.Zero)
                {
                    Logger.LogInfo($"AdvancedPaste: using MainWindowHandle for pid {proc.Id}.");
                    ShowWindowAsync(handle, SW_RESTORE);
                    SetForegroundWindow(handle);
                    return true;
                }

                Logger.LogInfo($"AdvancedPaste: MainWindowHandle not ready for pid {proc.Id}; enumerating windows.");

                if (TryBringProcessWindowToFront(proc.Id))
                {
                    return true;
                }

                Logger.LogInfo($"AdvancedPaste: no window found for pid {proc.Id}.");
            }
        }
        catch
        {
        }

        return false;
    }

#pragma warning disable SA1310 // Field names should not contain underscore
    private const int SW_RESTORE = 9;
#pragma warning restore SA1310 // Field names should not contain underscore

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindowAsync(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    private delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);

    private static bool TryBringProcessWindowToFront(int pid)
    {
        try
        {
            var windowHandle = IntPtr.Zero;
            EnumWindows(
                (hwnd, lParam) =>
                {
                    var threadId = GetWindowThreadProcessId(hwnd, out var windowPid);
                    if (threadId == 0)
                    {
                        Logger.LogInfo("AdvancedPaste: GetWindowThreadProcessId returned 0.");
                        return true;
                    }

                    if (windowPid == pid)
                    {
                        windowHandle = hwnd;
                        return false;
                    }

                    return true;
                },
                IntPtr.Zero);

            if (windowHandle == IntPtr.Zero)
            {
                return false;
            }

            Logger.LogInfo($"AdvancedPaste: enumerated window handle for pid {pid}.");
            ShowWindowAsync(windowHandle, SW_RESTORE);
            SetForegroundWindow(windowHandle);
            return true;
        }
        catch (Exception ex)
        {
            Logger.LogError($"AdvancedPaste: failed to enumerate/activate window for pid {pid}: {ex}");
            return false;
        }
    }
}
