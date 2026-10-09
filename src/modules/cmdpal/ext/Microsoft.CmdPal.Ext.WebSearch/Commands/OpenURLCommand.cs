// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Ext.WebSearch.Helpers.Browser;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.WebSearch.Commands;

internal sealed partial class OpenURLCommand : InvokableCommand
{
    private readonly IBrowserInfoService _browserInfoService;

    public string Url { get; internal set; } = string.Empty;

    internal OpenURLCommand(string url, IBrowserInfoService browserInfoService)
    {
        _browserInfoService = browserInfoService;
        Url = url;
        Icon = Icons.WebSearch;
        Name = string.Empty;
    }

    public override CommandResult Invoke()
    {
        // TODO GH# 138 --> actually display feedback from the extension somewhere.
        return _browserInfoService.Open(Url) ? CommandResult.Dismiss() : CommandResult.KeepOpen();
    }
}
