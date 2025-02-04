// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Tasks;
using Microsoft.CmdPal.Extensions.Helpers;
using Windows.Media.Control;

namespace MediaControlsExtension;

internal sealed partial class MediaListItem : CommandItem
{
    private GlobalSystemMediaTransportControlsSession _mediaSession;

    public MediaListItem()
        : base(new TogglePlayMediaCommand())
    {
        var task = GlobalSystemMediaTransportControlsSessionManager.RequestAsync().AsTask();
        task.ContinueWith(async t =>
        {
            var manager = t.Result;
            _mediaSession = manager.GetCurrentSession();
            ((TogglePlayMediaCommand)this.Command).MediaSession = _mediaSession;

            _mediaSession.MediaPropertiesChanged += MediaSession_MediaPropertiesChanged;
            _mediaSession.PlaybackInfoChanged += MediaSession_PlaybackInfoChanged;

            // mediaSession.TimelinePropertiesChanged += MediaSession_TimelinePropertiesChanged;
            await this.UpdateProperties();
        });

        // task.Start();
        MoreCommands = null;
    }

    private async Task UpdateProperties()
    {
        var properties = await this._mediaSession.TryGetMediaPropertiesAsync().AsTask();

        if (properties == null)
        {
            var a = (TogglePlayMediaCommand)this.Command;
            a.Icon = new(string.Empty);
            a.Name = "No media playing";

            return;
        }

        this.Title = properties.Title;

        // hack
        ((TogglePlayMediaCommand)this.Command).Name = this.Title;
        this.Subtitle = properties.Artist;
        var status = _mediaSession.GetPlaybackInfo().PlaybackStatus;

        var internalAction = (TogglePlayMediaCommand)this.Command;
        if (status == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Paused)
        {
            internalAction.Icon = new("\ue768"); // play
            internalAction.Name = "Paused";
        }
        else if (status == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing)
        {
            internalAction.Icon = new("\ue769"); // pause
            internalAction.Name = "Playing";
        }

        MoreCommands = [
            new CommandContextItem(new PrevNextTrackCommand(true, _mediaSession)),
            new CommandContextItem(new PrevNextTrackCommand(false, _mediaSession))
        ];
    }

    private void MediaSession_TimelinePropertiesChanged(GlobalSystemMediaTransportControlsSession sender, TimelinePropertiesChangedEventArgs args)
    {
        _ = UpdateProperties();
    }

    private void MediaSession_PlaybackInfoChanged(GlobalSystemMediaTransportControlsSession sender, PlaybackInfoChangedEventArgs args)
    {
        _ = UpdateProperties();
    }

    private void MediaSession_MediaPropertiesChanged(GlobalSystemMediaTransportControlsSession sender, MediaPropertiesChangedEventArgs args)
    {
        _ = UpdateProperties();
    }
}
