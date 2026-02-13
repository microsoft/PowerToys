// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.CmdPal.Ext.WebSearch.Commands;
using Microsoft.CmdPal.Ext.WebSearch.Helpers;
using Microsoft.CmdPal.Ext.WebSearch.Helpers.Browser;
using Microsoft.CmdPal.Ext.WebSearch.Pages;
using Microsoft.CmdPal.Ext.WebSearch.Properties;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.WebSearch;

public partial class WebSearchTopLevelCommandItem : CommandItem, IFallbackHandler, IDisposable
{
    private readonly SettingsManager _settingsManager;
    private readonly IBrowserInfoService _browserInfoService;

    public WebSearchTopLevelCommandItem(SettingsManager settingsManager, IBrowserInfoService browserInfoService)
        : base(new WebSearchListPage(settingsManager, browserInfoService))
    {
        Icon = Icons.WebSearch;
        SetDefaultTitle();
        _settingsManager = settingsManager;
        _browserInfoService = browserInfoService;
    }

    private void SetDefaultTitle() => Title = Resources.command_item_title;

    private void ReplaceCommand(ICommand newCommand)
    {
        (Command as IDisposable)?.Dispose();
        Command = newCommand;
    }

    public void UpdateQuery(string query)
    {
        if (string.IsNullOrEmpty(query))
        {
            SetDefaultTitle();
            ReplaceCommand(new WebSearchListPage(_settingsManager, _browserInfoService));
        }
        else
        {
            Title = query;
            ReplaceCommand(new SearchWebCommand(query, _settingsManager, _browserInfoService));
        }
    }

    public void Dispose()
    {
        (Command as IDisposable)?.Dispose();
        GC.SuppressFinalize(this);
    }
}
