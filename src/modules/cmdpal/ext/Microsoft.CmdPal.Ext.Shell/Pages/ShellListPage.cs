// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Ext.Shell.Helpers;
using Microsoft.CmdPal.Ext.Shell.Properties;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.Shell.Pages;

internal sealed partial class ShellListPage : DynamicListPage
{
    private readonly ShellListPageHelpers _helper;

    public ShellListPage(SettingsManager settingsManager)
    {
        Icon = Icons.RunV2;
        Id = "com.microsoft.cmdpal.shell";
        Name = Resources.cmd_plugin_name;
        PlaceholderText = Resources.list_placeholder_text;
        _helper = new(settingsManager);
    }

    public override void UpdateSearchText(string oldSearch, string newSearch) => RaiseItemsChanged(0);

    public override IListItem[] GetItems() => [.. _helper.Query(SearchText)];
}
