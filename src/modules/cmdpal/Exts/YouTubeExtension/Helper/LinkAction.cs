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

namespace YouTubeExtension.Helper;

internal sealed partial class LinkAction : InvokableCommand
{
    private readonly YouTubeVideo _video;

    internal LinkAction(YouTubeVideo video)
    {
        this._video = video;
        this.Name = "Open link";
        this.Icon = new("\uE8A7");
    }

    public override CommandResult Invoke()
    {
        Process.Start(new ProcessStartInfo(_video.Link) { UseShellExecute = true });
        return CommandResult.KeepOpen();
    }
}
