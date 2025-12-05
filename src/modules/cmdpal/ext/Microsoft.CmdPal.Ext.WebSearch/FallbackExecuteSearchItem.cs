// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using System.Text;
using Microsoft.CmdPal.Ext.WebSearch.Helpers;
using Microsoft.CmdPal.Ext.WebSearch.Helpers.Browser;
using Microsoft.CmdPal.Ext.WebSearch.Properties;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.WebSearch.Commands;

internal sealed partial class FallbackExecuteSearchItem : FallbackCommandItem
{
    private readonly SearchWebCommand _executeItem;
    private static readonly CompositeFormat PluginOpen = System.Text.CompositeFormat.Parse(Properties.Resources.plugin_open);
    private static readonly CompositeFormat SubtitleText = System.Text.CompositeFormat.Parse(Properties.Resources.web_search_fallback_subtitle);

    private readonly IBrowserInfoService _browserInfoService;

    public FallbackExecuteSearchItem(ISettingsInterface settings, IBrowserInfoService browserInfoService)
        : base(new SearchWebCommand(string.Empty, settings, browserInfoService) { Id = "com.microsoft.websearch.fallback" }, Resources.command_item_title)
    {
        _executeItem = (SearchWebCommand)Command!;
        _browserInfoService = browserInfoService;
        Title = string.Empty;
        Subtitle = string.Empty;
        _executeItem.Name = string.Empty;
        Icon = Icons.WebSearch;
    }

    private static string UpdateBrowserName(IBrowserInfoService browserInfoService)
    {
        var browserName = browserInfoService.GetDefaultBrowser()?.Name;
        return string.IsNullOrWhiteSpace(browserName)
            ? Resources.open_in_default_browser
            : string.Format(CultureInfo.CurrentCulture, PluginOpen, browserName);
    }

    public override void UpdateQuery(string query)
    {
        _executeItem.Arguments = query;
        var isEmpty = string.IsNullOrEmpty(query);
        _executeItem.Name = isEmpty ? string.Empty : Resources.open_in_default_browser;
        Title = isEmpty ? string.Empty : UpdateBrowserName(_browserInfoService);
        Subtitle = string.Format(CultureInfo.CurrentCulture, SubtitleText, query);
    }
}
