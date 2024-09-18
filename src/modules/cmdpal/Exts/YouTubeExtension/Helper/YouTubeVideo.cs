// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace YouTubeExtension.Helper;

public sealed class YouTubeVideo
{
    // The title of the video
    public string Title { get; init; } = string.Empty;

    // The URL link to the video
    public string Link { get; init; } = string.Empty;

    // The author or channel name of the video
    public string Author { get; init; } = string.Empty;

    // The channel id (needed for the channel URL)
    public string ChannelId { get; set; }

    // The URL link to the channel
    public string ChannelUrl { get; set; }

    // The URL to the thumbnail image of the video
    public string ThumbnailUrl { get; init; } = string.Empty;

    // Captions or subtitles associated with the video
    public string Captions { get; init; } = string.Empty;

    // The date and time the video was published
    public DateTime PublishedAt { get; set; }
}
