// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.CmdPal.Ext.WindowsSettings.Classes;
using Microsoft.CmdPal.Ext.WindowsSettings.Helpers;
using Microsoft.CmdPal.Extensions;
using Microsoft.CmdPal.Extensions.Helpers;

namespace Microsoft.CmdPal.Ext.WindowsSettings;

public partial class WindowsSettingsCommandsProvider : ICommandProvider
{
    public string DisplayName => $"Windows Services";

    private readonly ListItem _searchSettingsListItem;

#pragma warning disable CS8632 // The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.
    private readonly WindowsSettings.Classes.WindowsSettings? _windowsSettings;
#pragma warning restore CS8632 // The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.

    public WindowsSettingsCommandsProvider()
    {
        _windowsSettings = JsonSettingsListHelper.ReadAllPossibleSettings();
        _searchSettingsListItem = new ListItem(new WindowsSettingsListPage(_windowsSettings))
        {
            Title = "Search Windows Settings",
            Subtitle = "Quickly navigate to specific Windows settings",
        };

        UnsupportedSettingsHelper.FilterByBuild(_windowsSettings);

        TranslationHelper.TranslateAllSettings(_windowsSettings);
        WindowsSettingsPathHelper.GenerateSettingsPathValues(_windowsSettings);
    }

    public IconDataType Icon => new(string.Empty);

#pragma warning disable CA1816 // Dispose methods should call SuppressFinalize
    public void Dispose() => throw new NotImplementedException();
#pragma warning restore CA1816 // Dispose methods should call SuppressFinalize

    public IListItem[] TopLevelCommands()
    {
        return [
            _searchSettingsListItem
        ];
    }
}
