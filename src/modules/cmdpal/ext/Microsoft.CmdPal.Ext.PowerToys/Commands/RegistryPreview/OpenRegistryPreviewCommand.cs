// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using Microsoft.CommandPalette.Extensions.Toolkit;
using PowerToysExtension.Helpers;

namespace PowerToysExtension.Commands;

/// <summary>
/// Launches Registry Preview via the shared event.
/// </summary>
internal sealed partial class OpenRegistryPreviewCommand : InvokableCommand
{
    public OpenRegistryPreviewCommand()
    {
        Name = "Open Registry Preview";
    }

    public override CommandResult Invoke()
    {
        try
        {
            using var evt = new EventWaitHandle(false, EventResetMode.AutoReset, PowerToysEventNames.RegistryPreviewTrigger);
            evt.Set();
            return CommandResult.Dismiss();
        }
        catch (Exception ex)
        {
            return CommandResult.ShowToast($"Failed to open Registry Preview: {ex.Message}");
        }
    }
}
