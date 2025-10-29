// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Ext.RemoteDesktop.Properties;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.RemoteDesktop.Pages;

public sealed partial class FallbackRemoteDesktopItem : FallbackCommandItem
{
    public FallbackRemoteDesktopItem()
        : base(new NoOpCommand(), Resources.remotedesktop_title)
    {
        Title = string.Empty;
        Subtitle = string.Empty;
        Icon = Icons.RDPIcon;
    }

    public override void UpdateQuery(string query)
    {
    }
}
