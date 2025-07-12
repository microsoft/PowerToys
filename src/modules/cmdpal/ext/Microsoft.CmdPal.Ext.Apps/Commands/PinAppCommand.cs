// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics.CodeAnalysis;
using Microsoft.CmdPal.Ext.Apps.Helpers;
using Microsoft.CmdPal.Ext.Apps.Properties;
using Microsoft.CmdPal.Ext.Apps.State;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.Apps.Commands;

internal sealed partial class PinAppCommand : InvokableCommand
{
    private readonly string _appIdentifier;

    public PinAppCommand(string appIdentifier)
    {
        _appIdentifier = appIdentifier;
        Name = Resources.pin_app;
        Icon = Icons.PinIcon;
    }

    public override CommandResult Invoke()
    {
        PinnedAppsManager.Instance.PinApp(_appIdentifier);
        return CommandResult.KeepOpen();
    }
}
