// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.CmdPal.Extensions;
using Microsoft.CmdPal.Extensions.Helpers;

namespace Microsoft.CmdPal.Ext.Settings;

public class SettingsActionProvider : ICommandProvider
{
    public string DisplayName => $"Settings";

    private readonly SettingsPage settingsPage = new();

    public SettingsActionProvider()
    {
    }

    public IconDataType Icon => new(string.Empty);

#pragma warning disable CA1816 // Dispose methods should call SuppressFinalize
    public void Dispose() => throw new NotImplementedException();
#pragma warning restore CA1816 // Dispose methods should call SuppressFinalize

    public IListItem[] TopLevelCommands()
    {
        return [new ListItem(settingsPage) { Subtitle = "CmdPal settings" }];
    }
}
