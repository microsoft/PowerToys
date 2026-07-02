// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.CmdPal.Ext.Power.Helpers;
using Microsoft.CmdPal.Ext.Power.Properties;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.Power.Commands;

internal sealed partial class SetPowerPlanCommand : InvokableCommand
{
    private readonly PowerPlanService _service;
    private readonly Guid _schemeGuid;
    private readonly string _displayName;
    private readonly Action _onChanged;
    private readonly bool _dismissOnSuccess;

    internal SetPowerPlanCommand(
        PowerPlanService service,
        Guid schemeGuid,
        string displayName,
        Action onChanged,
        bool dismissOnSuccess = false)
    {
        _service = service;
        _schemeGuid = schemeGuid;
        _displayName = displayName;
        _onChanged = onChanged;
        _dismissOnSuccess = dismissOnSuccess;
        Id = $"com.microsoft.cmdpal.power.setPlan.{schemeGuid:B}";
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

        var message = Resources.power_plan_set_toast_prefix + PowerPlanDisplayHelper.GetPlanTitle(_schemeGuid, _displayName);
        return ShowToast(message, _dismissOnSuccess);
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
