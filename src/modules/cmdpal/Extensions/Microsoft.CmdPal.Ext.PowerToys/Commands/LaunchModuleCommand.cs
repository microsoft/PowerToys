// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Threading;
using Microsoft.CommandPalette.Extensions.Toolkit;
using PowerToysExtension.Helpers;

namespace PowerToysExtension.Commands;

/// <summary>
/// Launches a PowerToys module either by raising its shared event or starting the backing executable.
/// </summary>
internal sealed partial class LaunchModuleCommand : InvokableCommand
{
    private readonly string _moduleName;
    private readonly string _eventName;
    private readonly string _executableName;
    private readonly string _arguments;

    internal LaunchModuleCommand(
        string moduleName,
        string eventName = "",
        string executableName = "",
        string arguments = "",
        string displayName = "")
    {
        if (string.IsNullOrWhiteSpace(moduleName))
        {
            throw new ArgumentException("Module name is required", nameof(moduleName));
        }

        _moduleName = moduleName;
        _eventName = eventName ?? string.Empty;
        _executableName = executableName ?? string.Empty;
        _arguments = arguments ?? string.Empty;
        Name = string.IsNullOrWhiteSpace(displayName) ? $"Launch {moduleName}" : displayName;
    }

    public override CommandResult Invoke()
    {
        try
        {
            if (TrySignalEvent())
            {
                return CommandResult.Hide();
            }

            if (TryLaunchExecutable())
            {
                return CommandResult.Hide();
            }

            return CommandResult.ShowToast($"Unable to launch {_moduleName}.");
        }
        catch (Exception ex)
        {
            return CommandResult.ShowToast($"Launching {_moduleName} failed: {ex.Message}");
        }
    }

    private bool TrySignalEvent()
    {
        if (string.IsNullOrEmpty(_eventName))
        {
            return false;
        }

        try
        {
            using var existingHandle = EventWaitHandle.OpenExisting(_eventName);
            return existingHandle.Set();
        }
        catch (WaitHandleCannotBeOpenedException)
        {
            try
            {
                using var newHandle = new EventWaitHandle(false, EventResetMode.AutoReset, _eventName, out _);
                return newHandle.Set();
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

    private bool TryLaunchExecutable()
    {
        if (string.IsNullOrEmpty(_executableName))
        {
            return false;
        }

        var executablePath = PowerToysPathResolver.TryResolveExecutable(_executableName);
        if (string.IsNullOrEmpty(executablePath))
        {
            return false;
        }

        var startInfo = new ProcessStartInfo(executablePath)
        {
            UseShellExecute = true,
        };

        if (!string.IsNullOrWhiteSpace(_arguments))
        {
            startInfo.Arguments = _arguments;
            startInfo.UseShellExecute = false;
        }

        Process.Start(startInfo);
        return true;
    }
}
