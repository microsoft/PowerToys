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

internal sealed partial class YouTubeChannelInfoMarkdownPage : ContentPage
{
    private readonly YouTubeChannel _channel;
    private string _markdown = string.Empty;

    public YouTubeChannelInfoMarkdownPage(YouTubeChannel channel)
    {
        Icon = new IconInfo("\uE946");
        Name = "See more information about this channel";
        _channel = channel;
    }

    public override IContent[] GetContent()
    {
        var state = File.ReadAllText(YouTubeHelper.StateJsonPath());
        var jsonState = JsonNode.Parse(state);
        var apiKey = jsonState["apiKey"]?.ToString() ?? string.Empty;

        FillInChannelDetailsAsync(_channel, apiKey).GetAwaiter().GetResult();

        // Define the markdown content using the channel information
        _markdown = $@"
# {_channel.Name}

![Profile Picture]({_channel.ProfilePicUrl})

---

**Channel Description**

{_channel.Description}

---

**Key Stats**

- **Subscribers:** {_channel.SubscriberCount:N0}
- **Total Views:** {_channel.ViewCount:N0}
- **Total Videos:** {_channel.VideoCount:N0}

[Visit Channel]({_channel.ChannelUrl})

---

_Last updated: {DateTime.Now:MMMM dd, yyyy}_
_Data sourced via YouTube API_
";

        return [new MarkdownContent(_markdown)];
    }

    private async Task<YouTubeChannel> FillInChannelDetailsAsync(YouTubeChannel channel, string apiKey)
    {
        try
        {
            using var httpClient = new HttpClient();

            // Fetch channel details from YouTube API
            var channelUrl = $"https://www.googleapis.com/youtube/v3/channels?part=snippet,statistics&id={channel.ChannelId}&key={apiKey}";
            var channelResponse = await httpClient.GetStringAsync(channelUrl);
            var channelData = JsonNode.Parse(channelResponse);

            if (channelData?["items"]?.AsArray().Count > 0)
            {
                var channelSnippet = channelData?["items"]?[0]?["snippet"];
                var channelStatistics = channelData?["items"]?[0]?["statistics"];

                // Update statistics
                channel.ViewCount = long.TryParse(channelStatistics?["viewCount"]?.ToString(), out var views) ? views : 0;
                channel.VideoCount = long.TryParse(channelStatistics?["videoCount"]?.ToString(), out var videos) ? videos : 0;
            }
        }
        catch (Exception ex)
        {
            // Handle exceptions (e.g., log the error)
            Console.WriteLine($"An error occurred while fetching channel details: {ex.Message}");
        }

        return channel;
    }
}
