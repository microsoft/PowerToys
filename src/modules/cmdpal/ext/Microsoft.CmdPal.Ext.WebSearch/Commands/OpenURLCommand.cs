// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Ext.WebSearch.Helpers;
using Microsoft.CommandPalette.Extensions.Toolkit;

using BrowserInfo = Microsoft.CmdPal.Ext.WebSearch.Helpers.DefaultBrowserInfo;

namespace Microsoft.CmdPal.Ext.WebSearch.Commands;

internal sealed partial class OpenURLCommand : InvokableCommand
{
    private readonly SettingsManager _settingsManager;

    public string Url { get; internal set; } = string.Empty;

    internal OpenURLCommand(string url, SettingsManager settingsManager)
    {
        Url = url;
        BrowserInfo.UpdateIfTimePassed();
        Icon = IconHelpers.FromRelativePath("Assets\\WebSearch.png");
        Name = string.Empty;
        _settingsManager = settingsManager;
    }

    public override CommandResult Invoke()
    {
        if (!ShellHelpers.OpenCommandInShell(BrowserInfo.Path, BrowserInfo.ArgumentsPattern, $"{Url}"))
        {
            // TODO GH# 138 --> actually display feedback from the extension somewhere.
            return CommandResult.KeepOpen();
        }

        return CommandResult.Dismiss();
    }
}
