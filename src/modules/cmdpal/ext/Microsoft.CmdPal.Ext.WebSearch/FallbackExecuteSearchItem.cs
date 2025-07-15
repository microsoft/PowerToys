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
    private static readonly CompositeFormat SubtitleText = System.Text.CompositeFormat.Parse(Properties.Resources.web_search_fallback_subtitle);
    private string _title;

    public FallbackExecuteSearchItem(SettingsManager settings)
        : base(new SearchWebCommand(string.Empty, settings) { Id = "com.microsoft.websearch.fallback" }, Resources.command_item_title)
    {
        _executeItem = (SearchWebCommand)this.Command!;
        Title = string.Empty;
        Subtitle = string.Empty;
        _executeItem.Name = string.Empty;
        _title = string.Format(CultureInfo.CurrentCulture, PluginOpen, BrowserInfo.Name ?? BrowserInfo.MSEdgeName);
        Icon = IconHelpers.FromRelativePath("Assets\\WebSearch.png");
    }

    public override void UpdateQuery(string query)
    {
        _executeItem.Arguments = query;
        var isEmpty = string.IsNullOrEmpty(query);
        _executeItem.Name = isEmpty ? string.Empty : Properties.Resources.open_in_default_browser;
        Title = isEmpty ? string.Empty : _title;
        Subtitle = string.Format(CultureInfo.CurrentCulture, SubtitleText, query);
    }
}
