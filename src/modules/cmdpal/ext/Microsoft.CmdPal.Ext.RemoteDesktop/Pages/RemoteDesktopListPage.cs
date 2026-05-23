// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using Microsoft.CmdPal.Ext.RemoteDesktop.Commands;
using Microsoft.CmdPal.Ext.RemoteDesktop.Helper;
using Microsoft.CmdPal.Ext.RemoteDesktop.Properties;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.RemoteDesktop.Pages;

internal sealed partial class RemoteDesktopListPage : DynamicListPage
{
    private static readonly UriHostNameType[] ValidUriHostNameTypes = [
        UriHostNameType.IPv6,
        UriHostNameType.IPv4,
        UriHostNameType.Dns,
    ];

    private readonly IRdpConnectionsManager _rdpConnectionsManager;
    private ConnectionListItem? _arbitraryHostItem;

    public RemoteDesktopListPage(IRdpConnectionsManager rdpConnectionsManager)
    {
        Icon = Icons.RDPIcon;
        Name = Resources.remotedesktop_title;
        Id = "com.microsoft.cmdpal.builtin.remotedesktop";

        _rdpConnectionsManager = rdpConnectionsManager;
    }

    public override void UpdateSearchText(string oldSearch, string newSearch)
    {
        var query = newSearch?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(query))
        {
            _arbitraryHostItem = null;
            RaiseItemsChanged(0);
            return;
        }

        var connections = _rdpConnectionsManager.Connections.Where(w => !string.IsNullOrWhiteSpace(w.ConnectionName));
        var existingMatch = ConnectionHelpers.FindConnection(query, connections);

        if (existingMatch is not null && string.Equals(existingMatch.ConnectionName, query, StringComparison.OrdinalIgnoreCase))
        {
            // Exact match — no need for an arbitrary host item
            _arbitraryHostItem = null;
        }
        else if (IsValidHostname(query))
        {
            _arbitraryHostItem = new ConnectionListItem(query);
        }
        else
        {
            _arbitraryHostItem = null;
        }

        RaiseItemsChanged(0);
    }

    public override IListItem[] GetItems()
    {
        var connections = _rdpConnectionsManager.Connections.ToArray();

        if (_arbitraryHostItem is null)
        {
            return connections;
        }

        // Prepend the arbitrary host item so it appears at the top
        var result = new IListItem[connections.Length + 1];
        result[0] = _arbitraryHostItem;
        connections.CopyTo(result, 1);
        return result;
    }

    private static bool IsValidHostname(string query)
    {
        // Strip port suffix (e.g. "host:3389") before validation,
        // since Uri.CheckHostName does not accept host:port strings.
        var hostForValidation = query;
        var lastColon = hostForValidation.LastIndexOf(':');
        if (lastColon > 0 && lastColon < hostForValidation.Length - 1)
        {
            var portPart = hostForValidation.Substring(lastColon + 1);
            if (ushort.TryParse(portPart, out _))
            {
                hostForValidation = hostForValidation.Substring(0, lastColon);
            }
        }

        return ValidUriHostNameTypes.Contains(Uri.CheckHostName(hostForValidation));
    }
}
