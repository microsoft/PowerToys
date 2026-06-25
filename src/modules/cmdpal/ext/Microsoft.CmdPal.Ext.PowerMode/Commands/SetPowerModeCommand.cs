// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.CmdPal.Ext.PowerMode.Helpers;
using Microsoft.CmdPal.Ext.PowerMode.Properties;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.PowerMode.Commands;

internal sealed partial class SetPowerModeCommand : InvokableCommand
{
    private readonly PowerModeService _service;
    private readonly UserPowerMode _mode;
    private readonly string _successToast;
    private readonly Action _onChanged;

    internal SetPowerModeCommand(
        PowerModeService service,
        UserPowerMode mode,
        string successToast,
        Action onChanged)
    {
        _service = service;
        _mode = mode;
        _successToast = successToast;
        _onChanged = onChanged;
        Id = mode switch
        {
            UserPowerMode.BestEfficiency => "com.microsoft.cmdpal.powermode.setEfficiency",
            UserPowerMode.Balanced => "com.microsoft.cmdpal.powermode.setBalanced",
            UserPowerMode.BestPerformance => "com.microsoft.cmdpal.powermode.setPerformance",
            _ => "com.microsoft.cmdpal.powermode.setUnknown",
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

        return string.IsNullOrWhiteSpace(_successToast)
            ? CommandResult.KeepOpen()
            : ShowToastKeepOpen(_successToast);
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
