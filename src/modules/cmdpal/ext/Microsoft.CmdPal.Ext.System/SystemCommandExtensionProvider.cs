// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Ext.System.Helpers;
using Microsoft.CmdPal.Ext.System.Pages;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.System;

public sealed partial class SystemCommandExtensionProvider : CommandProvider
{
    private readonly ICommandItem[] _commands;
    private static readonly SettingsManager _settingsManager = new();
    public static readonly SystemCommandPage Page = new(_settingsManager);
    private readonly FallbackSystemCommandItem _fallbackSystemItem = new(_settingsManager);

    public SystemCommandExtensionProvider()
    {
        DisplayName = Resources.Microsoft_plugin_ext_system_page_name;
        Id = "com.microsoft.cmdpal.builtin.system";
        _commands = [
            new CommandItem(Page)
            {
                Title = Resources.Microsoft_plugin_ext_system_page_title,
                Icon = Page.Icon,
                MoreCommands = [new CommandContextItem(_settingsManager.Settings.SettingsPage)],
            },
        ];

        Icon = Page.Icon;
        Settings = _settingsManager.Settings;
    }

    public override ICommandItem[] TopLevelCommands()
    {
        return _commands;
    }

    public override IFallbackCommandItem[] FallbackCommands() => [_fallbackSystemItem];
}
