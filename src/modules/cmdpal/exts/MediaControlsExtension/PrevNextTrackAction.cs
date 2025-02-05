// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.Media.Control;

namespace MediaControlsExtension;

internal sealed partial class PrevNextTrackAction : InvokableCommand
{
    private readonly GlobalSystemMediaTransportControlsSession _mediaSession;
    private readonly bool _previous;

    public PrevNextTrackAction(bool previous, GlobalSystemMediaTransportControlsSession s)
    {
        _mediaSession = s;
        _previous = previous;

        if (previous)
        {
            Name = "Previous track";
            Icon = new IconInfo("\ue892");
        }
        else
        {
            Name = "Next track";
            Icon = new IconInfo("\ue893");
        }
    }

    public override ICommandResult Invoke()
    {
        if (_previous)
        {
            _ = _mediaSession.TrySkipPreviousAsync();
        }
        else
        {
            _ = _mediaSession.TrySkipNextAsync();
        }

        return CommandResult.KeepOpen();
    }
}
