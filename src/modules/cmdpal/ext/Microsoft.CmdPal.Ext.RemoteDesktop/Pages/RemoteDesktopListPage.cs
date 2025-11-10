// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Linq;
using Microsoft.CmdPal.Ext.RemoteDesktop.Helper;
using Microsoft.CmdPal.Ext.RemoteDesktop.Properties;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.RemoteDesktop.Pages;

internal sealed partial class RemoteDesktopListPage : ListPage
{
    private readonly IRdpConnectionManager _rdpConnectionManager;

    public RemoteDesktopListPage(IRdpConnectionManager rdpConnectionManager)
    {
        Icon = Icons.RDPIcon;
        Name = Resources.remotedesktop_title;
        Id = "com.microsoft.cmdpal.builtin.remotedesktop";

        _rdpConnectionManager = rdpConnectionManager;
    }

    public override IListItem[] GetItems() => _rdpConnectionManager.Connections.ToArray();
}
