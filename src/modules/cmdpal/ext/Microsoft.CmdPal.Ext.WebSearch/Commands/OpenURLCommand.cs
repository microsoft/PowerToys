// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.CmdPal.Ext.WebSearch.Helpers.Browser;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.WebSearch.Commands;

internal sealed partial class OpenURLCommand : InvokableCommand2
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
        => OpenUrl(Url);

    public override ICommandResult InvokeWithArgs(object? sender, IFallbackCommandInvocationArgs args)
        => TryNormalizeUrl(args.Query, out var normalizedUrl) ? OpenUrl(normalizedUrl) : CommandResult.KeepOpen();

    internal static bool TryNormalizeUrl(string query, out string normalizedUrl)
    {
        normalizedUrl = string.Empty;
        if (string.IsNullOrWhiteSpace(query))
        {
            return false;
        }

        if (Uri.IsWellFormedUriString(query, UriKind.Absolute))
        {
            normalizedUrl = query;
            return true;
        }

        if (query.Contains('.', StringComparison.OrdinalIgnoreCase) &&
            Uri.IsWellFormedUriString("https://" + query, UriKind.Absolute))
        {
            normalizedUrl = "https://" + query;
            return true;
        }

        return false;
    }

    private CommandResult OpenUrl(string url)
    {
        // TODO GH# 138 --> actually display feedback from the extension somewhere.
        return _browserInfoService.Open(url) ? CommandResult.Dismiss() : CommandResult.KeepOpen();
    }
}
