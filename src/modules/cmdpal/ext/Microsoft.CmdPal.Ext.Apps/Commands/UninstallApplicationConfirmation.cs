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

internal sealed partial class UninstallApplicationConfirmation : InvokableCommand
{
    private readonly string? _win32DisplayName;
    private readonly string? _packagedDisplayName;
    private readonly string? _packageFullName;

    /// <summary>Initializes a new instance of the <see cref="UninstallApplicationConfirmation"/> class. Creates an uninstall confirmation for a desktop app handled by Windows settings.</summary>
    public UninstallApplicationConfirmation(string win32DisplayName)
    {
        ArgumentNullException.ThrowIfNull(win32DisplayName);

        Name = Resources.uninstall_application;
        Icon = Icons.UninstallApplicationIcon;
        _win32DisplayName = win32DisplayName;
    }

    /// <summary>Initializes a new instance of the <see cref="UninstallApplicationConfirmation"/> class. Creates an uninstall confirmation for the supplied packaged app and its full package identity.</summary>
    public UninstallApplicationConfirmation(string displayName, string packageFullName)
    {
        ArgumentNullException.ThrowIfNull(displayName);
        ArgumentNullException.ThrowIfNull(packageFullName);

        Name = Resources.uninstall_application;
        Icon = Icons.UninstallApplicationIcon;
        _packagedDisplayName = displayName;
        _packageFullName = packageFullName;
    }

    public override CommandResult Invoke()
    {
        UninstallApplicationCommand uninstallCommand;

        var applicationTitle = Resources.uninstall_application;

        if (_win32DisplayName is not null)
        {
            uninstallCommand = new UninstallApplicationCommand(_win32DisplayName);
            applicationTitle = _win32DisplayName;
        }
        else if (_packagedDisplayName is not null && _packageFullName is not null)
        {
            uninstallCommand = new UninstallApplicationCommand(_packagedDisplayName, _packageFullName);
            applicationTitle = _packagedDisplayName;
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
