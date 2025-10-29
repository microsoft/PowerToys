// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.CmdPal.Ext.RemoteDesktop.Commands;
using Microsoft.CmdPal.Ext.RemoteDesktop.Helper;
using Microsoft.CmdPal.Ext.RemoteDesktop.Properties;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.Win32;

namespace Microsoft.CmdPal.Ext.RemoteDesktop.Pages;

public sealed partial class RemoteDesktopListPage : DynamicListPage
{
    private readonly RDPConnections _rdpConnections = RDPConnections.Create([]);

    private readonly List<ConnectionListItem> _items = new();

    public RemoteDesktopListPage()
    {
        Icon = Icons.RDPIcon;
        Name = Resources.remotedesktop_title;
        PlaceholderText = Resources.remotedesktop_subtitle;
        Id = "com.microsoft.cmdpal.builtin.remotedesktop";

        EmptyContent = new CommandItem(new NoOpCommand())
        {
            Icon = Icons.RDPIcon,
            Title = Resources.remotedesktop_title,
            Subtitle = Resources.remotedesktop_no_connections,
        };
        UpdateSearchText(string.Empty, string.Empty);
    }

    public override void UpdateSearchText(string oldSearch, string newSearch)
    {
        if (string.Equals(oldSearch, newSearch, System.StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _items.Clear();

        _rdpConnections.Reload(GetRdpConnectionsFromRegistry());

        var connections = _rdpConnections.FindConnections(newSearch) ?? Enumerable.Empty<Scored<string>>();

        _items.AddRange(connections.OrderBy(o => o.Score).Select(MapToResult));

        RaiseItemsChanged();
    }

    private ConnectionListItem MapToResult(Scored<string> item) => new(item.Item);

    // private static IReadOnlyCollection<string> GetRdpConnectionsFromRegistry()
    private static string[] GetRdpConnectionsFromRegistry()
    {
        var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Terminal Server Client\Default");
        if (key is null)
        {
            return [];
        }

        return [.. key.GetValueNames().Select(x => key.GetValue(x.Trim())?.ToString()).Where(value => value != null).Cast<string>()];
    }

    public override IListItem[] GetItems() => _items.ToArray();

    public ICommandItem ToCommandItem()
    {
        return new CommandItem(this);
    }
}
