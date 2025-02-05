// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions.Toolkit;
using YouTubeExtension.Helper;

namespace YouTubeExtension.Commands;

internal sealed partial class GetVideoInfoCommand : InvokableCommand
{
    private readonly YouTubeVideo _video;

    internal GetVideoInfoCommand(YouTubeVideo video)
    {
        this._video = video;
        this.Name = "See more information";
        this.Icon = new IconInfo("\uE946");
    }

    public override CommandResult Invoke() => CommandResult.KeepOpen();
}
