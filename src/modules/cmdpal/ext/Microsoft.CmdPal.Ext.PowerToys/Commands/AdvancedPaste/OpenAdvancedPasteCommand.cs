// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Threading;
using ManagedCommon;
using Microsoft.CommandPalette.Extensions.Toolkit;
using PowerToys.Interop;

namespace PowerToysExtension.Commands;

/// <summary>
/// Opens Advanced Paste UI by signaling the module's show event if the process is running.
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
            if (TrySignalAdvancedPaste())
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

    private static bool TrySignalAdvancedPaste()
    {
        try
        {
            var processes = Process.GetProcessesByName("PowerToys.AdvancedPaste");
            foreach (var proc in processes)
            {
                if (proc.HasExited)
                {
                    continue;
                }

                using var showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, Constants.AdvancedPasteShowUIEvent());
                showEvent.Set();
                Logger.LogInfo($"AdvancedPaste: signaled ShowUI for pid {proc.Id}.");
                return true;
            }
        }
        catch (Exception ex)
        {
            Logger.LogInfo($"AdvancedPaste: event activation failed: {ex.Message}");
        }

        return false;
    }
}
