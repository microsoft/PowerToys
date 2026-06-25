// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.CmdPal.Ext.PowerMode.Helpers;
using Microsoft.CmdPal.Ext.PowerMode.Properties;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.PowerMode.Commands;

internal sealed partial class SetPowerPlanCommand : InvokableCommand
{
    private readonly PowerPlanService _service;
    private readonly Guid _schemeGuid;
    private readonly string _displayName;
    private readonly Action _onChanged;

    internal SetPowerPlanCommand(
        PowerPlanService service,
        Guid schemeGuid,
        string displayName,
        Action onChanged)
    {
        _service = service;
        _schemeGuid = schemeGuid;
        _displayName = displayName;
        _onChanged = onChanged;
        Id = $"com.microsoft.cmdpal.powermode.setPlan.{schemeGuid:B}";
        Name = PowerPlanDisplayHelper.GetPlanTitle(schemeGuid, displayName);
        Icon = Icons.PlanGlyph(schemeGuid);
    }

    public override CommandResult Invoke()
    {
        if (!_service.TrySetActivePlan(_schemeGuid, out var error))
        {
            return ShowToastKeepOpen(error ?? Resources.power_plan_set_failed);
        }

        _onChanged();

        return ShowToastKeepOpen(Resources.power_plan_set_toast_prefix + PowerPlanDisplayHelper.GetPlanTitle(_schemeGuid, _displayName));
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
