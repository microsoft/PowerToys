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
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.WebSearch;

internal sealed partial class FallbackOpenURLItem : FormattedFallbackCommandItem, IHostMatchedFallbackCommandItem
{
    private const string FallbackId = "com.microsoft.cmdpal.builtin.websearch.openurl.fallback";
    private const string MatchPattern = @"(?:(?:https?|ftp|file)://)?[^\s]+\.[^\s]+";

    private readonly IBrowserInfoService _browserInfoService;
    private readonly OpenURLCommand _executeItem;
    private static readonly CompositeFormat PluginOpenURL = System.Text.CompositeFormat.Parse(Properties.Resources.plugin_open_url);
    private static readonly CompositeFormat PluginOpenUrlInBrowser = System.Text.CompositeFormat.Parse(Properties.Resources.plugin_open_url_in_browser);

    public FallbackOpenURLItem(ISettingsInterface settings, IBrowserInfoService browserInfoService)
        : base(
            new OpenURLCommand(string.Empty, browserInfoService),
            Resources.open_url_fallback_title,
            FallbackId,
            titleTemplate: string.Empty,
            subtitleTemplate: string.Empty)
    {
        ArgumentNullException.ThrowIfNull(browserInfoService);

        _browserInfoService = browserInfoService;
        _executeItem = (OpenURLCommand)Command!;
        Title = string.Empty;
        _executeItem.Name = string.Empty;
        Subtitle = string.Empty;
        Icon = Icons.WebSearch;
    }

    public override string TitleTemplate => Resources.plugin_open_url.Replace("{0}", "{query}", StringComparison.Ordinal);

    public override string SubtitleTemplate => GetSubtitle();

    public HostMatchKind MatchKind => HostMatchKind.Regex;

    public string MatchValue => MatchPattern;

    public override void UpdateQuery(string query)
    {
        if (!OpenURLCommand.TryNormalizeUrl(query, out var normalizedUrl))
        {
            _executeItem.Url = string.Empty;
            _executeItem.Name = string.Empty;
            Title = string.Empty;
            Subtitle = string.Empty;
            return;
        }

        _executeItem.Url = normalizedUrl;
        _executeItem.Name = string.IsNullOrEmpty(normalizedUrl) ? string.Empty : Resources.open_in_default_browser;

        Title = string.Format(CultureInfo.CurrentCulture, PluginOpenURL, normalizedUrl);
        Subtitle = GetSubtitle();
    }

    private string GetSubtitle()
    {
        var browserName = _browserInfoService.GetDefaultBrowser()?.Name;
        return string.IsNullOrWhiteSpace(browserName)
            ? Resources.open_in_default_browser
            : string.Format(CultureInfo.CurrentCulture, PluginOpenUrlInBrowser, browserName);
    }
}
