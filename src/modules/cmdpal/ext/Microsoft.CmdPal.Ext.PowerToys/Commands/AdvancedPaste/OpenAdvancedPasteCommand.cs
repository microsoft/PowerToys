// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.CommandPalette.Extensions.Toolkit;
using PowerToysExtension.Helpers;

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
            // If an instance is already running, just bring it to front.
            if (TryActivateExisting())
            {
                return CommandResult.GoHome("Advanced Paste opened");
            }

            var installPath = PowerToysPathResolver.GetPowerToysInstallPath();
            if (string.IsNullOrWhiteSpace(installPath))
            {
                return CommandResult.ShowToast("PowerToys install path not found.");
            }

            var exePath = Path.Combine(installPath, "WinUI3Apps", "PowerToys.AdvancedPaste.exe");
            if (!File.Exists(exePath))
            {
                return CommandResult.ShowToast("Advanced Paste executable not found.");
            }

            var pipeName = $"powertoys_advanced_paste_{Guid.NewGuid()}";

            using var server = new NamedPipeServerStream(
                pipeName,
                PipeDirection.Out,
                1,
                PipeTransmissionMode.Message,
                PipeOptions.Asynchronous);

            var psi = new ProcessStartInfo(exePath)
            {
                Arguments = $"{Process.GetCurrentProcessId()} {pipeName}",
                WorkingDirectory = installPath,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            var proc = Process.Start(psi);
            if (proc is null)
            {
                return CommandResult.ShowToast("Failed to start Advanced Paste.");
            }

            if (!server.WaitForConnectionAsync().Wait(TimeSpan.FromSeconds(5)))
            {
                return CommandResult.ShowToast("Advanced Paste did not respond.");
            }

            using var writer = new StreamWriter(server, Encoding.Unicode, leaveOpen: true)
            {
                AutoFlush = true,
            };

            writer.Write("ShowUI\r\n");

            return CommandResult.GoHome("Advanced Paste opened");
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
            foreach (var proc in Process.GetProcessesByName("PowerToys.AdvancedPaste"))
            {
                if (proc.HasExited)
                {
                    continue;
                }

                // Wait briefly for a main window if it hasn't been created yet.
                var handle = proc.MainWindowHandle;
                var waitUntil = DateTime.UtcNow + TimeSpan.FromMilliseconds(750);
                while (handle == IntPtr.Zero && DateTime.UtcNow < waitUntil)
                {
                    proc.Refresh();
                    handle = proc.MainWindowHandle;
                    if (handle != IntPtr.Zero)
                    {
                        break;
                    }
                    System.Threading.Thread.Sleep(50);
                }

                if (handle == IntPtr.Zero)
                {
                    continue;
                }

                ShowWindowAsync(handle, SW_RESTORE);
                SetForegroundWindow(handle);
                return true;
            }
        }
        catch
        {
        }

        return false;
    }

    private const int SW_RESTORE = 9;

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindowAsync(IntPtr hWnd, int nCmdShow);
}
