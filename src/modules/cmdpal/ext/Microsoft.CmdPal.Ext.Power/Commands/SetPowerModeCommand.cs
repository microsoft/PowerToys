// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.CmdPal.Ext.Power.Enumerations;
using Microsoft.CmdPal.Ext.Power.Helpers;
using Microsoft.CmdPal.Ext.Power.Properties;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.Power.Commands;

internal sealed partial class SetPowerModeCommand : InvokableCommand
{
    private readonly PowerModeService _service;
    private readonly UserPowerMode _mode;
    private readonly string _successToast;
    private readonly Action _onChanged;
    private readonly bool _dismissOnSuccess;

    internal SetPowerModeCommand(
        PowerModeService service,
        UserPowerMode mode,
        string successToast,
        Action onChanged,
        bool dismissOnSuccess = false)
    {
        _service = service;
        _mode = mode;
        _successToast = successToast;
        _onChanged = onChanged;
        _dismissOnSuccess = dismissOnSuccess;
        Id = mode switch
        {
            UserPowerMode.BestEfficiency => "com.microsoft.cmdpal.power.setEfficiency",
            UserPowerMode.Balanced => "com.microsoft.cmdpal.power.setBalanced",
            UserPowerMode.BestPerformance => "com.microsoft.cmdpal.power.setPerformance",
            _ => "com.microsoft.cmdpal.power.setUnknown",
        };
        Name = PowerModeDisplayHelper.GetUserModeLabel(mode);
        Icon = Icons.Glyph(mode);
    }

    public override CommandResult Invoke()
    {
        if (!_service.TrySetUserPowerMode(_mode, out var error))
        {
            return ShowToastKeepOpen(error ?? Resources.power_mode_set_failed);
        }

        _onChanged();

        if (string.IsNullOrWhiteSpace(_successToast))
        {
            return _dismissOnSuccess ? CommandResult.Dismiss() : CommandResult.KeepOpen();
        }

        return ShowToast(_successToast, _dismissOnSuccess);
    }

    private static CommandResult ShowToastKeepOpen(string message) => ShowToast(message, dismissOnSuccess: false);

    private static CommandResult ShowToast(string message, bool dismissOnSuccess)
    {
        return CommandResult.ShowToast(new ToastArgs()
        {
            Message = message,
            Result = dismissOnSuccess ? CommandResult.Dismiss() : CommandResult.KeepOpen(),
        });
    }
}
