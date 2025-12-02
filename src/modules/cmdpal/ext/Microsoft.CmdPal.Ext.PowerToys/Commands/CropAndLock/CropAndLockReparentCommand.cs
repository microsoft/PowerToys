// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using Microsoft.CommandPalette.Extensions.Toolkit;
using PowerToys.Interop;

namespace PowerToysExtension.Commands;

/// <summary>
/// Triggers Crop and Lock reparent mode via the shared event.
/// </summary>
internal sealed partial class CropAndLockReparentCommand : InvokableCommand
{
    public CropAndLockReparentCommand()
    {
        Name = "Crop and Lock (Reparent)";
    }

        public override CommandResult Invoke()
        {
            try
            {
                using var evt = new EventWaitHandle(false, EventResetMode.AutoReset, Constants.CropAndLockReparentEvent());
                evt.Set();
                return CommandResult.Dismiss();
            }
        catch (Exception ex)
        {
            return CommandResult.ShowToast($"Failed to start Crop and Lock (Reparent): {ex.Message}");
        }
    }
}
