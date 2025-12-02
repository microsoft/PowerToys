// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using Microsoft.CommandPalette.Extensions.Toolkit;
using PowerToys.Interop;

namespace PowerToysExtension.Commands;

/// <summary>
/// Launches Environment Variables (user) via the shared event.
/// </summary>
internal sealed partial class OpenEnvironmentVariablesCommand : InvokableCommand
{
    public OpenEnvironmentVariablesCommand()
    {
        Name = "Open Environment Variables";
    }

        public override CommandResult Invoke()
        {
            try
            {
                using var evt = new EventWaitHandle(false, EventResetMode.AutoReset, Constants.ShowEnvironmentVariablesSharedEvent());
                evt.Set();
                return CommandResult.Dismiss();
            }
        catch (Exception ex)
        {
            return CommandResult.ShowToast($"Failed to open Environment Variables: {ex.Message}");
        }
    }
}
