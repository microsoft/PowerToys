// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CmdPal.Extensions.Helpers;

namespace YouTubeExtension.Actions;

internal sealed partial class OpenVideoLinkAction : InvokableCommand
{
    private readonly string _videourl;

    internal OpenVideoLinkAction(string url)
    {
        this._videourl = url;
        this.Name = "Open video";
        this.Icon = new("\uE714");
    }

    public override CommandResult Invoke()
    {
        Process.Start(new ProcessStartInfo(_videourl) { UseShellExecute = true });
        return CommandResult.KeepOpen();
    }
}
