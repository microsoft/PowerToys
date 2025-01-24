// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Extensions;
using Microsoft.CmdPal.Extensions.Helpers;

namespace Microsoft.CmdPal.Ext.WinGet;

public partial class WinGetExtensionCommandsProvider : CommandProvider
{
    public WinGetExtensionCommandsProvider()
    {
        DisplayName = "WinGet";
        Id = "WinGet";
        Icon = WinGetExtensionPage.WinGetIcon;

        _ = WinGetStatics.Manager;
    }

    private readonly ICommandItem[] _commands = [
        new ListItem(new WinGetExtensionPage()),

        // new ListItem(
        //    new Microsoft.CmdPal.Ext.WinGetPage("command-line") { Title = "tag:command-line" })
        // {
        //    Title = "Search for command-line packages",
        // },

        // new ListItem(new InstalledPackagesPage())
    ];

    public override ICommandItem[] TopLevelCommands() => _commands;

    public override void InitializeWithHost(IExtensionHost host)
    {
        WinGetExtensionHost.Instance.Initialize(host);
    }
}
