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
        DisplayName = Properties.Resources.winget_display_name;
        Id = "WinGet";
        Icon = WinGetExtensionPage.WinGetIcon;

        _ = WinGetStatics.Manager;
    }

    private readonly ICommandItem[] _commands = [
        new ListItem(new WinGetExtensionPage()),

         new ListItem(
            new WinGetExtensionPage(WinGetExtensionPage.ExtensionsTag) { Title = Properties.Resources.winget_install_extensions_title })
         {
            Title = Properties.Resources.winget_install_extensions_title,
            Subtitle = Properties.Resources.winget_install_extensions_subtitle,
         },

        new ListItem(
            new OpenUrlCommand("ms-windows-store://assoc/?Tags=AppExtension-com.microsoft.commandpalette"))
         {
            Title = Properties.Resources.winget_search_store_title,
            Icon = IconHelpers.FromRelativePaths("Assets\\Store.light.svg", "Assets\\Store.dark.svg"),
         },
    ];

    public override ICommandItem[] TopLevelCommands() => _commands;

    public override void InitializeWithHost(IExtensionHost host) => WinGetExtensionHost.Instance.Initialize(host);

    public void SetAllLookup(Func<string, ICommandItem?> callback) => WinGetStatics.AppSearchCallback = callback;
}
