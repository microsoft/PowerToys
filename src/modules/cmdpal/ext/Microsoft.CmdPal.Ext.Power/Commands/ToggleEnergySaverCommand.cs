// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.CmdPal.Ext.Power.Helpers;
using Microsoft.CmdPal.Ext.Power.Properties;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.Power.Commands;

internal sealed partial class ToggleEnergySaverCommand : InvokableCommand
{
    private readonly EnergySaverService _service;
    private readonly Action _onChanged;

    internal ToggleEnergySaverCommand(EnergySaverService service, Action onChanged)
    {
        _service = service;
        _onChanged = onChanged;
        Id = "com.microsoft.cmdpal.power.energySaver.toggle";
        Name = Resources.power_mode_energy_saver_title;
        Icon = Icons.EnergySaverIcon;
    }

    public override CommandResult Invoke()
    {
        var snapshot = _service.GetSnapshot();
        var enable = !snapshot.IsOn;
        var successToast = enable
            ? Resources.power_mode_energy_saver_turn_on_toast
            : Resources.power_mode_energy_saver_turn_off_toast;

        if (_service.TrySetEnergySaver(enable, out var error))
        {
            _onChanged();
            return ShowToastKeepOpen(successToast);
        }

        return ShowToastKeepOpen(error ?? Resources.power_mode_energy_saver_set_failed);
    }

    private static CommandResult ShowToastKeepOpen(string message)
    {
        return CommandResult.ShowToast(new ToastArgs()
        {
            Message = message,
            Result = CommandResult.KeepOpen(),
        });
    }
}
