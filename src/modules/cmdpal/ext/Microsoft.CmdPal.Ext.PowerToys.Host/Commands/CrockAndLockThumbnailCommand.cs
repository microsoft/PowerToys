// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions.Toolkit;
using PowerToys.Interop;

namespace Microsoft.CmdPal.Ext.PowerToys.Commands;

internal sealed partial class CrockAndLockThumbnailCommand : InvokableCommand
{
    public CrockAndLockThumbnailCommand()
    {
        Icon = IconHelpers.FromRelativePath("Assets\\CropAndLock.png");
        Name = "Crock and Lock (Thumbnail)";
        Id = "com.microsoft.cmdpal.powertoys.crockandlock.thumbnail";
    }

    public override CommandResult Invoke()
    {
        using var eventHandle = new EventWaitHandle(false, EventResetMode.AutoReset, Constants.CropAndLockThumbnailEvent());
        eventHandle.Set();
        return CommandResult.Hide();
    }
}
