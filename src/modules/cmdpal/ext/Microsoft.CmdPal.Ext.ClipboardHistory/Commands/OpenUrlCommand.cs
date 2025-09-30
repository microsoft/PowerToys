// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.ClipboardHistory.Commands;

internal sealed partial class OpenUrlCommand : InvokableCommand
{
    private readonly string _url;

    internal OpenUrlCommand(string url)
    {
        _url = url;
        Name = Properties.Resources.open_url_command_name;
        Icon = new IconInfo("\uE8A7"); // OpenInNewWindow
    }

    public override CommandResult Invoke()
    {
        ShellHelpers.OpenInShell(_url);
        return CommandResult.Dismiss();
    }
}
