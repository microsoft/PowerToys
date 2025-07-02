// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.CmdPal.Ext.WebSearch.Helpers;
using Microsoft.CmdPal.Ext.WebSearch.Properties;
using Microsoft.CommandPalette.Extensions.Toolkit;

using BrowserInfo = Microsoft.CmdPal.Ext.WebSearch.Helpers.DefaultBrowserInfo;

namespace Microsoft.CmdPal.Ext.WebSearch.Commands;

internal sealed partial class SearchWebCommand : InvokableCommand
{
    private readonly SettingsManager _settingsManager;

    public string Arguments { get; internal set; } = string.Empty;

    internal SearchWebCommand(string arguments, SettingsManager settingsManager)
    {
        Arguments = arguments;
        BrowserInfo.UpdateIfTimePassed();
        Icon = IconHelpers.FromRelativePath("Assets\\WebSearch.png");
        Name = Properties.Resources.open_in_default_browser;
        _settingsManager = settingsManager;
    }

    public override CommandResult Invoke()
    {
        if (!ShellHelpers.OpenCommandInShell(BrowserInfo.Path, BrowserInfo.ArgumentsPattern, $"? {Arguments}"))
        {
            // TODO GH# 138 --> actually display feedback from the extension somewhere.
            return CommandResult.KeepOpen();
        }

        if (_settingsManager.ShowHistory != Resources.history_none)
        {
            _settingsManager.SaveHistory(new HistoryItem(Arguments, DateTime.Now));
        }

        return CommandResult.Dismiss();
    }
}
