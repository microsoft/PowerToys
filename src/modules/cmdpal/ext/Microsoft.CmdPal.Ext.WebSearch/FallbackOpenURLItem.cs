// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.Text;
using Microsoft.CmdPal.Ext.WebSearch.Commands;
using Microsoft.CmdPal.Ext.WebSearch.Helpers;
using Microsoft.CmdPal.Ext.WebSearch.Helpers.Browser;
using Microsoft.CmdPal.Ext.WebSearch.Properties;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.WebSearch;

internal sealed partial class FallbackOpenURLItem : FallbackCommandItem
{
    private readonly IBrowserInfoService _browserInfoService;
    private readonly OpenURLCommand _executeItem;
    private static readonly CompositeFormat PluginOpenURL = System.Text.CompositeFormat.Parse(Properties.Resources.plugin_open_url);
    private static readonly CompositeFormat PluginOpenUrlInBrowser = System.Text.CompositeFormat.Parse(Properties.Resources.plugin_open_url_in_browser);

    public FallbackOpenURLItem(ISettingsInterface settings, IBrowserInfoService browserInfoService)
        : base(new OpenURLCommand(string.Empty, browserInfoService), Resources.open_url_fallback_title)
    {
        ArgumentNullException.ThrowIfNull(browserInfoService);

        _browserInfoService = browserInfoService;
        _executeItem = (OpenURLCommand)Command!;
        Title = string.Empty;
        _executeItem.Name = string.Empty;
        Subtitle = string.Empty;
        Icon = Icons.WebSearch;
    }

    public override void UpdateQuery(string query)
    {
        if (!IsValidUrl(query))
        {
            _executeItem.Url = string.Empty;
            _executeItem.Name = string.Empty;
            Title = string.Empty;
            Subtitle = string.Empty;
            return;
        }

        var success = Uri.TryCreate(query, UriKind.Absolute, out _);

        // if url not contain schema, add http:// by default.
        if (!success)
        {
            query = "https://" + query;
        }

        _executeItem.Url = query;
        _executeItem.Name = string.IsNullOrEmpty(query) ? string.Empty : Resources.open_in_default_browser;

        Title = string.Format(CultureInfo.CurrentCulture, PluginOpenURL, query);

        var browserName = _browserInfoService.GetDefaultBrowser()?.Name;
        Subtitle = string.IsNullOrWhiteSpace(browserName) ? Resources.open_in_default_browser : string.Format(CultureInfo.CurrentCulture, PluginOpenUrlInBrowser, browserName);
    }

    private static bool IsValidUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return false;
        }

        if (!url.Contains('.', StringComparison.OrdinalIgnoreCase))
        {
            // eg: 'com', 'org'. We don't think it's a valid url.
            // This can simplify the logic of checking if the url is valid.
            return false;
        }

        if (Uri.IsWellFormedUriString(url, UriKind.Absolute))
        {
            return true;
        }

        if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase) &&
            !url.StartsWith("ftp://", StringComparison.OrdinalIgnoreCase) &&
            !url.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
        {
            if (Uri.IsWellFormedUriString("https://" + url, UriKind.Absolute))
            {
                return true;
            }
        }

        return false;
    }
}
