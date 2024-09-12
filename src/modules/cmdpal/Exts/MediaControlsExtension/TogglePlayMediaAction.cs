// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Extensions.Helpers;
using Windows.Media.Control;

namespace MediaControlsExtension;

public sealed partial class TogglePlayMediaAction : InvokableCommand
{
    public GlobalSystemMediaTransportControlsSession MediaSession { get; set; }

    public TogglePlayMediaAction()
    {
        Name = "No media playing";
        Icon = new(string.Empty);
    }

    public override CommandResult Invoke()
    {
        if (MediaSession != null)
        {
            _ = MediaSession.TryTogglePlayPauseAsync();
        }

        return CommandResult.KeepOpen();
    }
}
