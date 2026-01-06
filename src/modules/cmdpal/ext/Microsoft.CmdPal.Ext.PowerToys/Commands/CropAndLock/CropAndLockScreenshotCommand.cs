// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CommandPalette.Extensions.Toolkit;
using PowerToys.Interop;

namespace PowerToysExtension.Commands;

/// <summary>
/// Triggers Crop and Lock screenshot mode via the shared event.
/// </summary>
internal sealed partial class CropAndLockScreenshotCommand : InvokableCommand
{
    public CropAndLockScreenshotCommand()
    {
        Name = "Crop and Lock (Screenshot)";
    }

    public override CommandResult Invoke()
    {
        Task.Run(async () =>
        {
            await Task.Delay(500);
            try
            {
                using var evt = new EventWaitHandle(false, EventResetMode.AutoReset, Constants.CropAndLockScreenshotEvent());
                evt.Set();
            }
            catch
            {
                // Ignore errors after dismissing
            }
        });

        return CommandResult.Dismiss();
    }
}
