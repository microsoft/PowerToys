// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.CmdPal.Ext.WebSearch.Helpers;
using Microsoft.CmdPal.Ext.WebSearch.Helpers.Browser;
using Microsoft.CmdPal.Ext.WebSearch.Properties;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.WebSearch.Commands;

internal sealed partial class SearchWebCommand : InvokableCommand
{
    private readonly ISettingsInterface _settingsManager;
    private readonly IBrowserInfoService _browserInfoService;

    public string Arguments { get; internal set; }

    internal SearchWebCommand(string arguments, ISettingsInterface settingsManager, IBrowserInfoService browserInfoService)
    {
        Arguments = arguments;
        Icon = Icons.WebSearch;
        Name = Resources.open_in_default_browser;
        _settingsManager = settingsManager;
        _browserInfoService = browserInfoService;
    }

    public override CommandResult Invoke()
    {
        var uri = BuildUri();

        if (!_browserInfoService.Open(uri))
        {
            // TODO GH# 138 --> actually display feedback from the extension somewhere.
            return CommandResult.KeepOpen();
        }

        // remember only the query, not the full URI
        if (_settingsManager.HistoryItemCount != 0)
        {
            _settingsManager.AddHistoryItem(new HistoryItem(Arguments, DateTime.Now));
        }

        return CommandResult.Dismiss();
    }

    private string BuildUri()
    {
        if (string.IsNullOrWhiteSpace(_settingsManager.CustomSearchUri))
        {
            return $"? " + Arguments;
        }

        // if the custom search URI contains query placeholder, replace it with the actual query
        // otherwise append the query to the end of the URI
        // support {query}, %query% or %s as placeholder
        var placeholderVariants = new[] { "{query}", "%query%", "%s" };
        foreach (var placeholder in placeholderVariants)
        {
            if (_settingsManager.CustomSearchUri.Contains(placeholder, StringComparison.OrdinalIgnoreCase))
            {
                return _settingsManager.CustomSearchUri.Replace(placeholder, Uri.EscapeDataString(Arguments), StringComparison.OrdinalIgnoreCase);
            }
        }

        // is this too smart?
        var separator = _settingsManager.CustomSearchUri.Contains('?') ? '&' : '?';
        return $"{_settingsManager.CustomSearchUri}{separator}q={Uri.EscapeDataString(Arguments)}";
    }
}
