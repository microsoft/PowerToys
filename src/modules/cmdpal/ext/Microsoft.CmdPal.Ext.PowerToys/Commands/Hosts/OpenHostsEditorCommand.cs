// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using Microsoft.CommandPalette.Extensions.Toolkit;
using PowerToys.Interop;

namespace PowerToysExtension.Commands;

/// <summary>
/// Launches Hosts File Editor via the shared event.
/// </summary>
internal sealed partial class OpenHostsEditorCommand : InvokableCommand
{
    public OpenHostsEditorCommand()
    {
        Name = "Open Hosts File Editor";
    }

        public override CommandResult Invoke()
        {
            try
            {
                using var evt = new EventWaitHandle(false, EventResetMode.AutoReset, Constants.ShowHostsSharedEvent());
                evt.Set();
                return CommandResult.Dismiss();
            }
        catch (Exception ex)
        {
            return CommandResult.ShowToast($"Failed to open Hosts File Editor: {ex.Message}");
        }
    }
}
