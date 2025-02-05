// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace YouTubeExtension.Commands;

internal sealed partial class OpenVideoLinkCommand : InvokableCommand
{
    private readonly string _videourl;

    internal OpenVideoLinkCommand(string url)
    {
        this._videourl = url;
        this.Name = "Open video";
        this.Icon = new IconInfo("\uE714");
    }

    public override CommandResult Invoke()
    {
        Process.Start(new ProcessStartInfo(_videourl) { UseShellExecute = true });
        return CommandResult.KeepOpen();
    }
}
