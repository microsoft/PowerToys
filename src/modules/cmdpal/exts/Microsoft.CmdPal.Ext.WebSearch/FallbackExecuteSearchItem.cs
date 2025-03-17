// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using System.Text;
using Microsoft.CmdPal.Ext.WebSearch.Helpers;
using Microsoft.CmdPal.Ext.WebSearch.Properties;
using Microsoft.CommandPalette.Extensions.Toolkit;
using BrowserInfo = Microsoft.CmdPal.Ext.WebSearch.Helpers.DefaultBrowserInfo;

namespace Microsoft.CmdPal.Ext.WebSearch.Commands;

internal sealed partial class FallbackExecuteSearchItem : FallbackCommandItem
{
    private readonly SearchWebCommand _executeItem;
    private static readonly CompositeFormat PluginOpen = System.Text.CompositeFormat.Parse(Properties.Resources.plugin_open);

    public FallbackExecuteSearchItem(SettingsManager settings)
        : base(new SearchWebCommand(string.Empty, settings), Resources.command_item_title)
    {
        _executeItem = (SearchWebCommand)this.Command!;
        Title = string.Empty;
        _executeItem.Name = string.Empty;
        Subtitle = string.Format(CultureInfo.CurrentCulture, PluginOpen, BrowserInfo.Name ?? BrowserInfo.MSEdgeName);
        Icon = IconHelpers.FromRelativePath("Assets\\WebSearch.png");
    }

    public override void UpdateQuery(string query)
    {
        _executeItem.Arguments = query;
        _executeItem.Name = string.IsNullOrEmpty(query) ? string.Empty : Properties.Resources.open_in_default_browser;
        Title = query;
    }
}
