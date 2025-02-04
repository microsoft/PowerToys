// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Microsoft.CmdPal.Extensions;
using Microsoft.CmdPal.Extensions.Helpers;
using YouTubeExtension.Commands;
using YouTubeExtension.Helper;

namespace YouTubeExtension.Pages;

internal sealed partial class YouTubeChannelVideosPage : DynamicListPage
{
    private readonly string _channelId;
    private readonly string _channelName;

    public YouTubeChannelVideosPage(string channelId = null, string channelName = null)
    {
        Icon = new("https://www.youtube.com/favicon.ico");
        Name = $"Search for Videos by {channelName ?? "Channel"}";
        this.ShowDetails = true;

        // Ensure either a ChannelId or ChannelName is provided
        if (string.IsNullOrEmpty(channelId) && string.IsNullOrEmpty(channelName))
        {
            throw new ArgumentException("Either channelId or channelName must be provided.");
        }

        _channelId = channelId;
        _channelName = channelName;
    }

    public override void UpdateSearchText(string oldSearch, string newSearch) => RaiseItemsChanged(0); // 0 is bodgy

    public override IListItem[] GetItems() => DoGetItems(SearchText).GetAwaiter().GetResult(); // Fetch and await the task synchronously

    private async Task<IListItem[]> DoGetItems(string query)
    {
        // Fetch YouTube videos scoped to the channel
        var items = await GetYouTubeChannelVideos(query, _channelId, _channelName);

        // Create a section and populate it with the video results
        var section = items.Select(video => new ListItem(new OpenVideoLinkCommand(video.Link))
        {
            Title = video.Title,
            Subtitle = $"{video.Channel}",
            Details = new Details()
            {
                Title = video.Title,
                HeroImage = new(video.ThumbnailUrl),
                Body = $"{video.Channel}",
            },
            Tags = [
                    new Tag()
                    {
                        Text = video.PublishedAt.ToString("MMMM dd, yyyy", CultureInfo.InvariantCulture), // Show the date of the video post
                    }
                ],
            MoreCommands = [
                    new CommandContextItem(new OpenChannelLinkCommand(video.ChannelUrl)),
                    new CommandContextItem(new YouTubeVideoInfoMarkdownPage(video)),
                    new CommandContextItem(new YouTubeAPIPage()),
                ],
        }).ToArray();

        return section;
    }

    // Method to fetch videos from a specific channel
    private static async Task<List<YouTubeVideo>> GetYouTubeChannelVideos(string query, string channelId, string channelName)
    {
        var state = File.ReadAllText(YouTubeHelper.StateJsonPath());
        var jsonState = JsonNode.Parse(state);
        var apiKey = jsonState["apiKey"]?.ToString() ?? string.Empty;

        var videos = new List<YouTubeVideo>();

        using var client = new HttpClient();
        {
            try
            {
                // Build the YouTube API URL for fetching channel-specific videos
                string requestUrl;

                if (!string.IsNullOrEmpty(channelId))
                {
                    // If ChannelId is provided, filter by channelId
                    requestUrl = $"https://www.googleapis.com/youtube/v3/search?part=snippet&type=video&channelId={channelId}&q={query}&key={apiKey}&maxResults=20";
                }
                else
                {
                    // If ChannelName is provided, search by the channel name
                    requestUrl = $"https://www.googleapis.com/youtube/v3/search?part=snippet&type=video&q={query}+{channelName}&key={apiKey}&maxResults=20";
                }

                // Send the request to the YouTube API
                var response = await client.GetStringAsync(requestUrl);
                var json = JsonNode.Parse(response);

                // Parse the response
                if (json?["items"] is JsonArray itemsArray)
                {
                    foreach (var item in itemsArray)
                    {
                        // Add each video to the list with title, link, author, thumbnail, and captions (if available)
                        videos.Add(new YouTubeVideo
                        {
                            Title = item["snippet"]?["title"]?.ToString() ?? string.Empty,
                            VideoId = item["id"]?["videoId"]?.ToString() ?? string.Empty,
                            Link = $"https://www.youtube.com/watch?v={item["id"]?["videoId"]?.ToString()}",
                            Channel = item["snippet"]?["channelTitle"]?.ToString() ?? string.Empty,
                            ChannelId = item["snippet"]?["channelId"]?.ToString() ?? string.Empty,
                            ChannelUrl = $"https://www.youtube.com/channel/{item["snippet"]?["channelId"]?.ToString()}" ?? string.Empty,
                            ThumbnailUrl = item["snippet"]?["thumbnails"]?["default"]?["url"]?.ToString() ?? string.Empty, // Get the default thumbnail URL
                            PublishedAt = DateTime.Parse(item["snippet"]?["publishedAt"]?.ToString(), CultureInfo.InvariantCulture), // Use CultureInfo.InvariantCulture
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                // Handle any errors from the API call or parsing
                videos.Add(new YouTubeVideo
                {
                    Title = "Error fetching data",
                    Channel = $"Error: {ex.Message}",
                });
            }
        }

        return videos;
    }
}
