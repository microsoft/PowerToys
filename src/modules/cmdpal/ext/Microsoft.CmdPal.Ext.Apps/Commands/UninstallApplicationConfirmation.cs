// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.Text;
using ManagedCommon;
using Microsoft.CmdPal.Ext.Apps.Programs;
using Microsoft.CmdPal.Ext.Apps.Properties;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.Apps.Commands;

internal sealed partial class UninstallApplicationConfirmation : InvokableCommand
{
    private readonly UWPApplication? _uwpTarget;
    private readonly Win32Program? _win32Target;

    public UninstallApplicationConfirmation(UWPApplication target)
    {
        Name = Resources.uninstall_application;
        Icon = Icons.UninstallApplicationIcon;
        _uwpTarget = target ?? throw new ArgumentNullException(nameof(target));
    }

    public UninstallApplicationConfirmation(Win32Program target)
    {
        Name = Resources.uninstall_application;
        Icon = Icons.UninstallApplicationIcon;
        _win32Target = target ?? throw new ArgumentNullException(nameof(target));
    }

    public override CommandResult Invoke()
    {
        UninstallApplicationCommand uninstallCommand;

        var applicationTitle = Resources.uninstall_application;

        if (_uwpTarget is not null)
        {
            uninstallCommand = new UninstallApplicationCommand(_uwpTarget);
            applicationTitle = _uwpTarget.DisplayName;
        }
        else if (_win32Target is not null)
        {
            uninstallCommand = new UninstallApplicationCommand(_win32Target);
            applicationTitle = _win32Target.Name;
        }
        else
        {
            Logger.LogError("UninstallApplicationCommand invoked with no target.");
            return CommandResult.Dismiss();
        }

        var confirmArgs = new ConfirmationArgs()
        {
            Title = string.Format(CultureInfo.CurrentCulture, CompositeFormat.Parse(Resources.uninstall_application_confirm_title), applicationTitle),
            Description = Resources.uninstall_application_confirm_description,
            PrimaryCommand = uninstallCommand,
            IsPrimaryCommandCritical = true,
        };

        return CommandResult.Confirm(confirmArgs);
    }
}
