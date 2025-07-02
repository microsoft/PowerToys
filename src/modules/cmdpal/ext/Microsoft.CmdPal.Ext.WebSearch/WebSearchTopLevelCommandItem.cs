// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using Microsoft.CmdPal.Ext.WebSearch.Commands;
using Microsoft.CmdPal.Ext.WebSearch.Helpers;
using Microsoft.CmdPal.Ext.WebSearch.Pages;
using Microsoft.CmdPal.Ext.WebSearch.Properties;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.WebSearch;

public partial class WebSearchTopLevelCommandItem : CommandItem, IFallbackHandler
{
    private readonly SettingsManager _settingsManager;

    public WebSearchTopLevelCommandItem(SettingsManager settingsManager)
        : base(new WebSearchListPage(settingsManager))
    {
        Icon = IconHelpers.FromRelativePath("Assets\\WebSearch.png");
        SetDefaultTitle();
        _settingsManager = settingsManager;
    }

    private void SetDefaultTitle() => Title = Resources.command_item_title;

    public void UpdateQuery(string query)
    {
        if (string.IsNullOrEmpty(query))
        {
            SetDefaultTitle();
            Command = new WebSearchListPage(_settingsManager);
        }
        else
        {
            Title = query;
            Command = new SearchWebCommand(query, _settingsManager);
        }
    }
}
