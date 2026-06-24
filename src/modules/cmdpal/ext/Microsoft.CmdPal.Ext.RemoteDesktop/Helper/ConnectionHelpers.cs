// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.CmdPal.Ext.RemoteDesktop.Commands;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.RemoteDesktop.Helper;

internal static class ConnectionHelpers
{
    public static ConnectionListItem MapToResult(string item) => new(item);

    public static ConnectionListItem? FindConnection(string query, IEnumerable<ConnectionListItem> connections)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return null;
        }

        var matchedConnection = ListHelpers.FilterList(
                                            connections,
                                            query,
                                            (s, i) => ListHelpers.ScoreListItem(s, i))
                                            .FirstOrDefault();
        return matchedConnection;
    }
}
