// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.Linq;
using System.Text;
using Microsoft.CmdPal.Ext.RemoteDesktop.Helper;
using Microsoft.CmdPal.Ext.RemoteDesktop.Properties;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.RemoteDesktop.Commands;

internal sealed partial class FallbackRemoteDesktopItem : FallbackCommandItem
{
    private const string _id = "com.microsoft.cmdpal.builtin.remotedesktop.fallback";

    private static readonly CompositeFormat RemoteDesktopOpenHostFormat = CompositeFormat.Parse(Resources.remotedesktop_open_host);

    private readonly IRdpConnectionsManager _rdpConnectionsManager;
    private readonly NoOpCommand _emptyCommand = new NoOpCommand();

    public FallbackRemoteDesktopItem(IRdpConnectionsManager rdpConnectionsManager)
    : base(Resources.remotedesktop_title, _id)
    {
        _rdpConnectionsManager = rdpConnectionsManager;

        SuggestedQueryDelayMilliseconds = new(true, 75);
        SuggestedMinQueryLength = new(true, 2);
        Command = _emptyCommand;
        Title = string.Empty;
        Subtitle = string.Empty;
        Icon = Icons.RDPIcon;
    }

    public override void UpdateQuery(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            Title = string.Empty;
            Subtitle = string.Empty;
            Command = _emptyCommand;
            return;
        }

        var connections = _rdpConnectionsManager.Connections.Where(w => !string.IsNullOrWhiteSpace(w.ConnectionName));

        var queryConnection = ConnectionHelpers.FindConnection(query, connections);

        if (queryConnection is not null && !string.IsNullOrWhiteSpace(queryConnection.ConnectionName))
        {
            var connectionName = queryConnection.ConnectionName;
            Command = new OpenRemoteDesktopCommand(connectionName);
            Title = connectionName;
            Subtitle = string.Format(CultureInfo.CurrentCulture, RemoteDesktopOpenHostFormat, connectionName);
        }
        else
        {
            Title = string.Empty;
            Subtitle = string.Empty;
            Command = _emptyCommand;
        }
    }
}
