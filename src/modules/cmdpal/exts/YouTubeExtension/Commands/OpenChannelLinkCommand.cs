// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using Microsoft.CmdPal.Extensions.Helpers;

namespace YouTubeExtension.Commands;

internal sealed partial class OpenChannelLinkCommand : InvokableCommand
{
    private readonly string _channelurl;

    internal OpenChannelLinkCommand(string url)
    {
        this._channelurl = url;
        this.Name = "Open channel";
        this.Icon = new("\uF131");
    }

    public override CommandResult Invoke()
    {
        Process.Start(new ProcessStartInfo(_channelurl) { UseShellExecute = true });
        return CommandResult.KeepOpen();
    }
}
