// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using Microsoft.CommandPalette.Extensions.Toolkit;
using PowerToysExtension.Helpers;

namespace PowerToysExtension.Commands;

/// <summary>
/// Triggers Crop and Lock thumbnail mode via the shared event.
/// </summary>
internal sealed partial class CropAndLockThumbnailCommand : InvokableCommand
{
    public CropAndLockThumbnailCommand()
    {
        Name = "Crop and Lock (Thumbnail)";
    }

    public override CommandResult Invoke()
    {
        try
        {
            using var evt = new EventWaitHandle(false, EventResetMode.AutoReset, PowerToysEventNames.CropAndLockThumbnail);
            evt.Set();
            return CommandResult.Dismiss();
        }
        catch (Exception ex)
        {
            return CommandResult.ShowToast($"Failed to start Crop and Lock (Thumbnail): {ex.Message}");
        }
    }
}
