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

        if (snapshot.State == ResolvedEnergySaverState.NotAvailable)
        {
            return ShowToastKeepOpen(Resources.power_mode_energy_saver_not_available);
        }

        if (enable && ShouldOpenQuickSettingsDirectly())
        {
            return OpenQuickSettingsFallback(Resources.power_mode_energy_saver_open_quick_settings_toast);
        }

        var successToast = enable
            ? Resources.power_mode_energy_saver_turn_on_toast
            : Resources.power_mode_energy_saver_turn_off_toast;

        if (_service.TrySetEnergySaver(enable, out _))
        {
            _onChanged();
            return ShowToast(successToast, dismissOnSuccess: false);
        }

        return OpenQuickSettingsFallback(
            enable
                ? Resources.power_mode_energy_saver_open_quick_settings_toast
                : Resources.power_mode_energy_saver_open_quick_settings_off_toast);
    }

    private static bool ShouldOpenQuickSettingsDirectly() =>
        EnergySaverStateHelper.HasRegistryRuntimeDrift();

    private static CommandResult OpenQuickSettingsFallback(string message)
    {
        if (EnergySaverFallbackHelper.TryOpenQuickSettings())
        {
            return ShowToast(message, dismissOnSuccess: true);
        }

        if (EnergySaverFallbackHelper.TryOpenPowerSettings())
        {
            return ShowToast(Resources.power_mode_energy_saver_open_settings_toast, dismissOnSuccess: true);
        }

        return ShowToastKeepOpen(Resources.power_mode_energy_saver_set_failed);
    }

    private static CommandResult ShowToastKeepOpen(string message) =>
        ShowToast(message, dismissOnSuccess: false);

    private static CommandResult ShowToast(string message, bool dismissOnSuccess)
    {
        return CommandResult.ShowToast(new ToastArgs()
        {
            Message = message,
            Result = dismissOnSuccess ? CommandResult.Dismiss() : CommandResult.KeepOpen(),
        });
    }
}
