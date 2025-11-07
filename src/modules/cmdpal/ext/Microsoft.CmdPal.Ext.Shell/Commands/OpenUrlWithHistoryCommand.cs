// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Core.Common.Services;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.Shell;

internal sealed partial class OpenUrlWithHistoryCommand : OpenUrlCommand
{
    private readonly Action<string>? _addToHistory;
    private readonly string _url;
    private readonly ITelemetryService? _telemetryService;

    public OpenUrlWithHistoryCommand(string url, Action<string>? addToHistory = null, ITelemetryService? telemetryService = null)
        : base(url)
    {
        _addToHistory = addToHistory;
        _url = url;
        _telemetryService = telemetryService;
    }

    public override CommandResult Invoke()
    {
        _addToHistory?.Invoke(_url);

        var success = ShellHelpers.OpenInShell(_url);
        var isWebUrl = false;

        if (Uri.TryCreate(_url, UriKind.Absolute, out var uri))
        {
            if (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
            {
                isWebUrl = true;
            }
        }

        _telemetryService?.LogOpenUri(_url, isWebUrl, success);

        return CommandResult.Dismiss();
    }
}
