// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace YouTubeExtension.Helper;

public sealed class YouTubeVideo
{
    // The title of the video
    public string Title { get; set; } = string.Empty;

    // The unique id of the video
    public string VideoId { get; set; } = string.Empty;

    // The URL link to the video
    public string Link { get; set; } = string.Empty;

    // The author or channel name of the video
    public string Channel { get; set; } = string.Empty;

    // The channel id (needed for the channel URL)
    public string ChannelId { get; set; } = string.Empty;

    // The URL link to the channel
    public string ChannelUrl { get; set; } = string.Empty;

    // The URL to the profile picture of the channel
    public string ChannelProfilePicUrl { get; set; }

    // Number of subscribers the channel has
    public long SubscriberCount { get; set; }

    // The URL to the thumbnail image of the video
    public string ThumbnailUrl { get; set; } = string.Empty;

    // Captions or subtitles associated with the video
    public string Caption { get; set; } = string.Empty;

    // The date and time the video was published
    public DateTime PublishedAt { get; set; } = DateTime.MinValue;

    // Number of views the video has
    public long ViewCount { get; set; }

    // Number of likes the video has
    public long LikeCount { get; set; }
}
