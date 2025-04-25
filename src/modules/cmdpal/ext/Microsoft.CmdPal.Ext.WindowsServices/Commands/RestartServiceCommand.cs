// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Resources;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CmdPal.Ext.WindowsServices.Helpers;
using Microsoft.CmdPal.Ext.WindowsServices.Properties;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.UI;

namespace Microsoft.CmdPal.Ext.WindowsServices.Commands;

internal sealed partial class RestartServiceCommand : InvokableCommand
{
    private readonly ServiceResult _serviceResult;

    internal RestartServiceCommand(ServiceResult serviceResult)
    {
        _serviceResult = serviceResult;
        Name = Resources.wox_plugin_service_restart;
        Icon = new IconInfo("\xE72C"); // Refresh icon
    }

    public override CommandResult Invoke()
    {
        Task.Run(() => ServiceHelper.ChangeStatus(_serviceResult, Action.Restart));

        return CommandResult.KeepOpen();
    }
}
