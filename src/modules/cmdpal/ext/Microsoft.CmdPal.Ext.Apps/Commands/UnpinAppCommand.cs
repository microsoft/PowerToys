// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Ext.Apps.Properties;
using Microsoft.CmdPal.Ext.Apps.State;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.Apps.Commands;

internal sealed partial class UnpinAppCommand : InvokableCommand
{
    private readonly string _appIdentifier;

    public UnpinAppCommand(string appIdentifier)
    {
        _appIdentifier = appIdentifier;
        Name = Resources.unpin_app;
        Icon = Icons.UnpinIcon;
    }

    public override CommandResult Invoke()
    {
        PinnedAppsManager.Instance.UnpinApp(_appIdentifier);
        return CommandResult.KeepOpen();
    }
}
