// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Ext.WindowsSettings.Helpers;
using Microsoft.CmdPal.Ext.WindowsSettings.Pages;
using Microsoft.CmdPal.Ext.WindowsSettings.Properties;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.WindowsSettings;

public sealed partial class WindowsSettingsCommandsProvider : CommandProvider
{
    private readonly CommandItem _searchSettingsListItem;

#pragma warning disable CS8632 // The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.
    private readonly WindowsSettings.Classes.WindowsSettings? _windowsSettings;
#pragma warning restore CS8632 // The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.

    private readonly FallbackWindowsSettingsItem _fallback;

    public WindowsSettingsCommandsProvider()
    {
        Id = "com.microsoft.cmdpal.builtin.windowssettings";
        DisplayName = Resources.WindowsSettingsProvider_DisplayName;
        Icon = Icons.WindowsSettingsIcon;

        _windowsSettings = JsonSettingsListHelper.ReadAllPossibleSettings();
        _searchSettingsListItem = new CommandItem(new WindowsSettingsListPage(_windowsSettings))
        {
            Title = Resources.settings_title,
            Subtitle = Resources.settings_subtitle,
        };
        _fallback = new(_windowsSettings);

        UnsupportedSettingsHelper.FilterByBuild(_windowsSettings);

        TranslationHelper.TranslateAllSettings(_windowsSettings);
        WindowsSettingsPathHelper.GenerateSettingsPathValues(_windowsSettings);
    }

    public override ICommandItem[] TopLevelCommands()
    {
        return [
            _searchSettingsListItem
        ];
    }

    public override IFallbackCommandItem[] FallbackCommands() => [_fallback];
}
