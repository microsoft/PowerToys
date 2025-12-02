// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using Microsoft.CommandPalette.Extensions.Toolkit;
using PowerToys.Interop;

namespace PowerToysExtension.Commands;

/// <summary>
/// Triggers Find My Mouse via the shared event.
/// </summary>
internal sealed partial class ToggleFindMyMouseCommand : InvokableCommand
{
    public ToggleFindMyMouseCommand()
    {
        Name = "Trigger Find My Mouse";
    }

        public override CommandResult Invoke()
        {
            try
            {
                using var evt = new EventWaitHandle(false, EventResetMode.AutoReset, Constants.FindMyMouseTriggerEvent());
                evt.Set();
                return CommandResult.Dismiss();
            }
        catch (Exception ex)
        {
            return CommandResult.ShowToast($"Failed to trigger Find My Mouse: {ex.Message}");
        }
    }
}
