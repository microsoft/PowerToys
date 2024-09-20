// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace YouTubeExtension.Actions;

public sealed class YouTubeChannel
{
    // The name of the channel
    public string Name { get; set; } = string.Empty;

    // The unique id of the channel
    public string ChannelId { get; set; } = string.Empty;

    // The URL link to the channel
    public string ChannelUrl { get; set; } = string.Empty;

    // The URL to the profile picture of the channel
    public string ProfilePicUrl { get; set; } = string.Empty;

    // The description of the channel
    public string Description { get; set; } = string.Empty;

    // Number of subscribers the channel has
    public long SubscriberCount { get; set; }

    // Number of views the channel has
    public long ViewCount { get; set; }

    // Number of videos the channel has
    public long VideoCount { get; set; }
}
