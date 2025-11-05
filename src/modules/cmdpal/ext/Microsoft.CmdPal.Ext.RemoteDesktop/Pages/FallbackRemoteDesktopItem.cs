// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Microsoft.CmdPal.Ext.RemoteDesktop.Commands;
using Microsoft.CmdPal.Ext.RemoteDesktop.Helper;
using Microsoft.CmdPal.Ext.RemoteDesktop.Properties;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.CmdPal.Ext.RemoteDesktop.Pages;

internal sealed partial class FallbackRemoteDesktopItem : FallbackCommandItem
{
    private const string _id = "com.microsoft.cmdpal.builtin.remotedesktop.fallback";

    // Cache the CompositeFormat for repeated use (CA1863)
    private static readonly CompositeFormat RemoteDesktopOpenHostFormat = CompositeFormat.Parse(Resources.remotedesktop_open_host);
    private readonly ServiceProvider _serviceProvider;

    public FallbackRemoteDesktopItem(ServiceProvider serviceProvider)
    : base(new OpenRemoteDesktopCommand(string.Empty), Resources.remotedesktop_title)
    {
        _serviceProvider = serviceProvider;

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
            Command = new OpenRemoteDesktopCommand(string.Empty);
            return;
        }

        List<ConnectionListItem> items = new();

        var rdpConnectionManager = _serviceProvider.GetRequiredService<RDPConnectionsManager>();
        rdpConnectionManager.Reload();

        var connections = rdpConnectionManager.FindConnections(query) ?? Enumerable.Empty<Scored<string>>();

        items.AddRange(connections.OrderBy(o => o.Score).Select(RDPConnectionsManager.MapToResult));

        if (items.Count > 0)
        {
            var connectionName = items[0].ConnectionName;

            Command = new OpenRemoteDesktopCommand(connectionName);
            Title = connectionName;
            Subtitle = string.Format(CultureInfo.CurrentCulture, RemoteDesktopOpenHostFormat, connectionName);
        }
        else
        {
            Title = string.Empty;
            Subtitle = string.Empty;
            Command = new OpenRemoteDesktopCommand(string.Empty);
        }
    }
}
