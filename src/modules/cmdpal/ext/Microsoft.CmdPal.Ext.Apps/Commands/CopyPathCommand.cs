// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using ManagedCommon;
using Microsoft.CmdPal.Ext.Apps.Properties;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.Apps.Commands;

internal sealed partial class CopyPathCommand : InvokableCommand
{
    private static readonly IconInfo TheIcon = new("\ue8c8");

    private readonly string _target;

    public CopyPathCommand(string target)
    {
        Name = Resources.copy_path;
        Icon = TheIcon;

        _target = target;
    }

    public override CommandResult Invoke()
    {
        try
        {
            ClipboardHelper.SetText(_target);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex.Message);
            return CommandResult.ShowToast(Resources.copy_failed + ": " + ex.Message);
        }

        return CommandResult.KeepOpen();
    }
}
