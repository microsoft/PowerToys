// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Threading;
using Microsoft.CommandPalette.Extensions.Toolkit;
using PowerToysExtension.Helpers;

namespace PowerToysExtension.Commands;

internal sealed partial class OpenWorkspaceEditorCommand : InvokableCommand
{
    private const string LaunchEditorEventName = "Local\\Workspaces-LaunchEditorEvent-a55ff427-cf62-4994-a2cd-9f72139296bf";

    public override CommandResult Invoke()
    {
        try
        {
            if (TrySignalLaunchEvent())
            {
                return CommandResult.Hide();
            }

            if (TryLaunchEditorExecutable())
            {
                return CommandResult.Hide();
            }

            if (TryLaunchThroughSettings())
            {
                return CommandResult.Hide();
            }

            return CommandResult.ShowToast("Unable to launch the Workspaces editor.");
        }
        catch (Exception ex)
        {
            return CommandResult.ShowToast($"Launching editor failed: {ex.Message}");
        }
    }

    private static bool TrySignalLaunchEvent()
    {
        try
        {
            using var existing = EventWaitHandle.OpenExisting(LaunchEditorEventName);
            return existing.Set();
        }
        catch (WaitHandleCannotBeOpenedException)
        {
            try
            {
                using var created = new EventWaitHandle(false, EventResetMode.AutoReset, LaunchEditorEventName, out _);
                return created.Set();
            }
            catch
            {
                return false;
            }
        }
        catch
        {
            return false;
        }
    }

    private static bool TryLaunchEditorExecutable()
    {
        var editorPath = PowerToysPathResolver.TryResolveExecutable("PowerToys.WorkspacesEditor.exe");
        if (string.IsNullOrEmpty(editorPath))
        {
            return false;
        }

        var startInfo = new ProcessStartInfo(editorPath)
        {
            UseShellExecute = true,
        };

        Process.Start(startInfo);
        return true;
    }

    private static bool TryLaunchThroughSettings()
    {
        var powerToysExe = PowerToysPathResolver.TryResolveExecutable("PowerToys.exe");
        if (string.IsNullOrEmpty(powerToysExe))
        {
            return false;
        }

        var startInfo = new ProcessStartInfo(powerToysExe)
        {
            Arguments = "--open-settings=Workspaces",
            UseShellExecute = false,
        };

        Process.Start(startInfo);
        return true;
    }
}
