// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.CommandPalette.Extensions.Toolkit;
using PowerToysExtension.Helpers;

namespace PowerToysExtension.Commands;

internal sealed partial class RefreshAwakeStatusCommand : InvokableCommand
{
    private readonly Action<string> _applySubtitle;

    internal RefreshAwakeStatusCommand(Action<string> applySubtitle)
    {
        ArgumentNullException.ThrowIfNull(applySubtitle);
        _applySubtitle = applySubtitle;
        Name = "Refresh Awake status";
    }

    public override CommandResult Invoke()
    {
        try
        {
            var subtitle = AwakeStatusService.GetStatusSubtitle();
            _applySubtitle(subtitle);
            return CommandResult.KeepOpen();
        }
        catch (Exception ex)
        {
            return CommandResult.ShowToast(new ToastArgs()
            {
                Message = $"Failed to refresh Awake status: {ex.Message}",
                Result = CommandResult.KeepOpen(),
            });
        }
    }
}
