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

internal sealed partial class GetVideoInfoAction : InvokableCommand
{
    private readonly YouTubeVideo _video;

    internal GetVideoInfoAction(YouTubeVideo video)
    {
        this._video = video;
        this.Name = "See more information";
        this.Icon = new("\uE946");
    }

    public override CommandResult Invoke()
    {
        return CommandResult.KeepOpen();
    }
}
