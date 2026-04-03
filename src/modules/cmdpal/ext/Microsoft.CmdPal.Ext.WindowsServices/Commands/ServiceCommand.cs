// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Tasks;
using Microsoft.CmdPal.Ext.WindowsServices.Helpers;
using Microsoft.CmdPal.Ext.WindowsServices.Properties;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.WindowsServices.Commands;

internal sealed partial class ServiceCommand : InvokableCommand
{
    private readonly ServiceResult _serviceResult;
    private readonly Action _action;

    internal ServiceCommand(ServiceResult serviceResult, Action action)
    {
        _serviceResult = serviceResult;
        _action = action;
        Name = action switch
        {
            Action.Start => Resources.wox_plugin_service_start,
            Action.Stop => Resources.wox_plugin_service_stop,
            Action.Restart => Resources.wox_plugin_service_restart,
            _ => throw new ArgumentOutOfRangeException(nameof(action), action, null),
        };

        Icon = serviceResult.IsRunning ? Icons.StopIcon : Icons.PlayIcon;
    }

    public override CommandResult Invoke()
    {
        Task.Run(() => ServiceHelper.ChangeStatus(_serviceResult, _action));

        return CommandResult.KeepOpen();
    }
}
