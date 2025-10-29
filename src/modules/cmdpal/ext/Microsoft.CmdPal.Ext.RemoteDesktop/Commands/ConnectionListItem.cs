// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.RemoteDesktop.Commands;

internal sealed partial class ConnectionListItem : ListItem
{
    public ConnectionListItem(string connectionName)
    {
        ConnectionName = connectionName;
        Title = connectionName;
        Subtitle = $"Connect to {connectionName} via RDP";
        Icon = Icons.RDPIcon;
        Command = new NoOpCommand();
    }

    public string ConnectionName { get; }

    public override string Subtitle => throw new System.NotImplementedException();

    public override string Title => throw new System.NotImplementedException();
}
