// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.Text;
using ManagedCommon;
using Microsoft.CmdPal.Ext.Apps.Properties;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.Apps.Commands;

internal sealed partial class CopyPathCommand : InvokableCommand
{
    private readonly string _target;

    public CopyPathCommand(string target)
    {
        Name = Resources.copy_path;
        Icon = Icons.CopyIcon;

        _target = target;
    }

    private static readonly CompositeFormat CopyFailedFormat = CompositeFormat.Parse(Resources.copy_failed);

    public override CommandResult Invoke()
    {
        try
        {
            ClipboardHelper.SetText(_target);
        }
        catch (Exception ex)
        {
            Logger.LogError("Copy failed: " + ex.Message);
            return CommandResult.ShowToast(
                new ToastArgs
                {
                    Message = string.Format(CultureInfo.CurrentCulture, CopyFailedFormat, ex.Message),
                    Result = CommandResult.KeepOpen(),
                });
        }

        return CommandResult.ShowToast(Resources.copied_to_clipboard);
    }
}
