// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Ext.Shell.Helpers;
using Microsoft.CmdPal.Ext.Shell.Properties;
using Microsoft.CmdPal.Extensions;
using Microsoft.CmdPal.Extensions.Helpers;

namespace Microsoft.CmdPal.Ext.Shell.Pages;

internal sealed partial class ShellListPage : DynamicListPage
{
    private readonly ShellListPageHelpers _helper;

    public ShellListPage(SettingsManager settingsManager)
    {
        Icon = new("\uE756");
        Id = "com.microsoft.cmdpal.shell";
        Name = Resources.cmd_plugin_name;
        _helper = new(settingsManager);
    }

    public override void UpdateSearchText(string oldSearch, string newSearch) => RaiseItemsChanged(0);

    public override IListItem[] GetItems() => [.. _helper.Query(SearchText)];
}
