// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Net.Http;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using YouTubeExtension.Helper;

namespace YouTubeExtension.Pages;

internal sealed partial class YouTubeVideoInfoMarkdownPage : ContentPage
{
    private readonly YouTubeVideo _video;
    private string _markdown = string.Empty;

    public YouTubeVideoInfoMarkdownPage(YouTubeVideo video)
    {
        Icon = new IconInfo("\uE946");
        Name = "See more information about this video";
        _video = video;
    }

    public override IContent[] GetContent()
    {
        var state = File.ReadAllText(YouTubeHelper.StateJsonPath());
        var jsonState = JsonNode.Parse(state);
        var apiKey = jsonState["apiKey"]?.ToString() ?? string.Empty;

        FillInVideoDetailsAsync(_video, apiKey).GetAwaiter().GetResult();

        // Refined markdown content for user-focused display
        _markdown = $@"
# {_video.Title}

![Thumbnail]({_video.ThumbnailUrl})

---

**Video Description**

{_video.Caption}

---

**Key Stats**

- **Views:** {_video.ViewCount:N0}
- **Likes:** {_video.LikeCount:N0}
- **Published on:** {_video.PublishedAt:MMMM dd, yyyy}

---

**Channel Info**

- **Channel Name:** {_video.Channel}
- **Subscribers:** {_video.SubscriberCount:N0}

![Channel Profile Picture]({_video.ChannelProfilePicUrl})

[Visit Channel]({_video.ChannelUrl})

---

[Watch Video]({_video.Link})

---

_Last updated: {DateTime.Now:MMMM dd, yyyy}_
_Data sourced via YouTube API_
";

        return [new MarkdownContent(_markdown)];
    }

    private async Task<YouTubeVideo> FillInVideoDetailsAsync(YouTubeVideo video, string apiKey)
    {
        try
        {
            using var httpClient = new HttpClient();

            // Fetch channel details to get ChannelProfilePicUrl and SubscriberCount
            var channelUrl = $"https://www.googleapis.com/youtube/v3/channels?part=snippet,statistics&id={video.ChannelId}&key={apiKey}";
            var channelResponse = await httpClient.GetStringAsync(channelUrl);
            var channelData = JsonNode.Parse(channelResponse);

            if (channelData?["items"]?.AsArray().Count > 0)
            {
                var channelSnippet = channelData?["items"]?[0]?["snippet"];
                var channelStatistics = channelData?["items"]?[0]?["statistics"];

                // Update ChannelProfilePicUrl and SubscriberCount
                video.ChannelProfilePicUrl = channelSnippet?["thumbnails"]?["default"]?["url"]?.ToString() ?? string.Empty;
                video.SubscriberCount = long.TryParse(channelStatistics?["subscriberCount"]?.ToString(), out var subscribers) ? subscribers : 0;
            }

            // Fetch video details to get ViewCount, LikeCount, and Captions (description)
            var videoUrl = $"https://www.googleapis.com/youtube/v3/videos?part=statistics,snippet&id={video.VideoId}&key={apiKey}";
            var videoResponse = await httpClient.GetStringAsync(videoUrl);
            var videoData = JsonNode.Parse(videoResponse);

            if (videoData?["items"]?.AsArray().Count > 0)
            {
                var videoSnippet = videoData?["items"]?[0]?["snippet"];
                var videoStatistics = videoData?["items"]?[0]?["statistics"];

                // Update ViewCount and LikeCount
                video.ViewCount = long.TryParse(videoStatistics?["viewCount"]?.ToString(), out var views) ? views : 0;
                video.LikeCount = long.TryParse(videoStatistics?["likeCount"]?.ToString(), out var likes) ? likes : 0;

                // Update Captions (description)
                video.Caption = videoSnippet?["description"]?.ToString() ?? string.Empty;
            }
        }
        catch (Exception ex)
        {
            // Handle errors
            Console.WriteLine($"An error occurred while fetching video details: {ex.Message}");
        }

        return video;
    }
}
