// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using System.Text;
using Microsoft.CmdPal.Ext.RemoteDesktop.Properties;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.RemoteDesktop.Commands;

internal sealed partial class ConnectionListItem : ListItem
{
    public ConnectionListItem(string connectionName)
    {
        ConnectionName = connectionName;

        if (string.IsNullOrEmpty(connectionName))
        {
            Title = Resources.remotedesktop_open_rdp;
            Subtitle = Resources.remotedesktop_subtitle;
        }
        else
        {
            Title = connectionName;
            CompositeFormat remoteDesktopOpenHostFormat = CompositeFormat.Parse(Resources.remotedesktop_open_host);
            Subtitle = string.Format(CultureInfo.CurrentCulture, remoteDesktopOpenHostFormat, connectionName);
        }

        Icon = Icons.RDPIcon;
        Command = new OpenRemoteDesktopCommand(connectionName);
    }

    public string ConnectionName { get; }
}
