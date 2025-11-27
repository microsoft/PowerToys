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
        if (!_browserInfoService.Open($"? {Arguments}"))
        {
            // TODO GH# 138 --> actually display feedback from the extension somewhere.
            return CommandResult.KeepOpen();
        }

        if (_settingsManager.HistoryItemCount != 0)
        {
            _settingsManager.AddHistoryItem(new HistoryItem(Arguments, DateTime.Now));
        }

        return CommandResult.Dismiss();
    }
}
