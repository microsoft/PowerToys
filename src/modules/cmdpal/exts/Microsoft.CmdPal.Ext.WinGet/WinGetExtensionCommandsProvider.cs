// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

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

         new ListItem(
            new WinGetExtensionPage(WinGetExtensionPage.ExtensionsTag) { Title = "Install Extensions" })
         {
            Title = "Install Command Palette extensions",
            Subtitle = "Search for extensions on WinGet",
         },

        new ListItem(
            new OpenUrlCommand("ms-windows-store://assoc/?Tags=AppExtension-com.microsoft.commandpalette"))
         {
            Title = "Search for extensions on the Store",
            Icon = IconHelpers.FromRelativePath("Assets\\ms-store.png"),
         },
    ];

    public override ICommandItem[] TopLevelCommands() => _commands;

    public override void InitializeWithHost(IExtensionHost host) => WinGetExtensionHost.Instance.Initialize(host);

    public void SetAllLookup(Func<string, ICommandItem?> callback) => WinGetStatics.AppSearchCallback = callback;
}
