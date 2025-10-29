// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Ext.RemoteDesktop.Pages;
using Microsoft.CmdPal.Ext.RemoteDesktop.Properties;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.RemoteDesktop;

public partial class RemoteDesktopCommandProvider : CommandProvider
{
    private RemoteDesktopListPage listPage = new();
    private FallbackRemoteDesktopItem fallback = new();

    public RemoteDesktopCommandProvider()
    {
        Id = "com.microsoft.cmdpal.builtin.remotedesktop";
        DisplayName = Resources.remotedesktop_title;
        Icon = Icons.RDPIcon;
    }

    public override ICommandItem[] TopLevelCommands() => [listPage.ToCommandItem()];

    public override IFallbackCommandItem[] FallbackCommands() => [fallback];
}
