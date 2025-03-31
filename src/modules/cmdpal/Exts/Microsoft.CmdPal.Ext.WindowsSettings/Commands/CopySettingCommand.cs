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
using Microsoft.CmdPal.Ext.WindowsSettings.Classes;
using Microsoft.CmdPal.Ext.WindowsSettings.Helpers;
using Microsoft.CmdPal.Ext.WindowsSettings.Properties;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.ApplicationModel.DataTransfer;
using Windows.Networking.NetworkOperators;
using Windows.UI;

namespace Microsoft.CmdPal.Ext.WindowsSettings.Commands;

internal sealed partial class CopySettingCommand : InvokableCommand
{
    private readonly WindowsSetting _entry;

    internal CopySettingCommand(WindowsSetting entry)
    {
        Name = Resources.CopyCommand;
        Icon = new IconInfo("\xE8C8"); // Copy icon
        _entry = entry;
    }

    public override CommandResult Invoke()
    {
        ClipboardHelper.SetText(_entry.Command);

        return CommandResult.Dismiss();
    }
}
