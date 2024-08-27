// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Threading.Tasks;
using Microsoft.Windows.CommandPalette.Extensions;
using Microsoft.Windows.CommandPalette.Extensions.Helpers;
using Windows.Media.Control;

namespace MediaControlsExtension;

internal sealed class PrevNextTrackAction : InvokableCommand
{
    private readonly GlobalSystemMediaTransportControlsSession mediaSession;
    private readonly bool previous;
    public PrevNextTrackAction(bool previous, GlobalSystemMediaTransportControlsSession s)
    {
        this.mediaSession = s;
        this.previous = previous;
        if (previous)
        {
            this.Name = "Previous track";
            this.Icon = new("\ue892");
        }
        else
        {
            this.Name = "Next track";
            this.Icon = new("\ue893");

        }
    }
    public override ICommandResult Invoke()
    {
        if (this.previous)
        {
            _ = this.mediaSession.TrySkipPreviousAsync();
        }
        else
        {
            _ = this.mediaSession.TrySkipNextAsync();
        }
        return ActionResult.KeepOpen();
    }
}

internal sealed class TogglePlayMediaAction : InvokableCommand
{
    internal GlobalSystemMediaTransportControlsSession mediaSession;
    public TogglePlayMediaAction()
    {
        Name = "No media playing";
        Icon = new("");
    }
    public override ActionResult Invoke()
    {
        if (mediaSession != null) {
            _ = mediaSession.TryTogglePlayPauseAsync();
        }
        return ActionResult.KeepOpen();
    }
}
internal sealed class MediaListItem : ListItem
{
    private GlobalSystemMediaTransportControlsSession mediaSession;
    public MediaListItem() : base(new TogglePlayMediaAction())
    {
        var task = GlobalSystemMediaTransportControlsSessionManager.RequestAsync().AsTask();
        task.ContinueWith(async t =>  {
            var manager = t.Result;
            mediaSession = manager.GetCurrentSession();
            ((TogglePlayMediaAction)this.Command).mediaSession = mediaSession;
            mediaSession.MediaPropertiesChanged += MediaSession_MediaPropertiesChanged;
            mediaSession.PlaybackInfoChanged += MediaSession_PlaybackInfoChanged;
            // mediaSession.TimelinePropertiesChanged += MediaSession_TimelinePropertiesChanged;
            await this.UpdateProperties();
        });
        // task.Start();

        this._MoreCommands = null;
    }
    private async Task UpdateProperties() {
        var properties = await this.mediaSession.TryGetMediaPropertiesAsync().AsTask();
        if (properties == null)
        {
            var a = ((TogglePlayMediaAction)this.Command);
            a.Icon = new("");
            a.Name = "No media playing";
            return;
        }

        this.Title = properties.Title;
        // hack
        ((TogglePlayMediaAction)this.Command).Name = this.Title;
        this.Subtitle = properties.Artist;
        var status = mediaSession.GetPlaybackInfo().PlaybackStatus;

        var internalAction = ((TogglePlayMediaAction)this.Command);
        if (status == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Paused)
        {
            internalAction.Icon = new("\ue768"); //play
            internalAction.Name = "Paused";
        }
        else if (status == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing)
        {
            internalAction.Icon = new("\ue769"); //pause
            internalAction.Name = "Playing";
        }

        this.MoreCommands = [
            new CommandContextItem(new PrevNextTrackAction(true, mediaSession)),
            new CommandContextItem(new PrevNextTrackAction(false, mediaSession))
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

public class MediaActionsProvider : ICommandProvider
{
    public string DisplayName => $"Media controls actions";
    public IconDataType Icon => new("");

    private readonly IListItem[] _Actions = [
        new MediaListItem()
    ];

#pragma warning disable CA1816 // Dispose methods should call SuppressFinalize
    public void Dispose() => throw new NotImplementedException();
#pragma warning restore CA1816 // Dispose methods should call SuppressFinalize


    public IListItem[] TopLevelCommands()
    {
        return _Actions;
    }
}

