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
using Microsoft.CmdPal.Extensions;
using Microsoft.CmdPal.Extensions.Helpers;
using Windows.UI;

namespace Microsoft.CmdPal.Ext.WindowsServices.Commands;

internal sealed partial class ServiceCommand : InvokableCommand
{
    private readonly ServiceResult _serviceResult;
    private readonly Action _action;

    internal ServiceCommand(ServiceResult serviceResult, Action action)
    {
        _serviceResult = serviceResult;
        _action = action;
        Name = action.ToString();
        if (serviceResult.IsRunning)
        {
            Icon = new("\xE71A"); // Stop icon
        }
        else
        {
            Icon = new("\xEDB5"); // Playbadge12 icon
        }
    }

    public override CommandResult Invoke()
    {
        Task.Run(() => ServiceHelper.ChangeStatus(_serviceResult, _action));

        return CommandResult.KeepOpen();
    }
}
