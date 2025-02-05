// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using YouTubeExtension.Commands;
using YouTubeExtension.Helper;

namespace YouTubeExtension.Pages;

internal sealed partial class YouTubeChannelsPage : DynamicListPage
{
    public YouTubeChannelsPage()
    {
        Icon = new IconInfo("https://www.youtube.com/favicon.ico");
        Name = "YouTube (Channel Search)";
        this.ShowDetails = true;
    }

    public override void UpdateSearchText(string oldSearch, string newSearch) => RaiseItemsChanged(0); // 0 is bodgy

    public override IListItem[] GetItems() => DoGetItems(SearchText).GetAwaiter().GetResult(); // Fetch and await the task synchronously

    private async Task<IListItem[]> DoGetItems(string query)
    {
        // Fetch YouTube channels based on the query
        var items = await GetYouTubeChannels(query);

        // Create a section and populate it with the channel results
        var section = items.Select(channel => new ListItem(new OpenChannelLinkCommand(channel.ChannelUrl))
        {
            Title = channel.Name,
            Subtitle = $"{channel.SubscriberCount} subscribers",
            Details = new Details()
            {
                Title = channel.Name,
                HeroImage = new IconInfo(channel.ProfilePicUrl),
                Body = $"Subscribers: {channel.SubscriberCount}\nChannel Description: {channel.Description}",
            },
            MoreCommands = [
                    new CommandContextItem(new YouTubeChannelInfoMarkdownPage(channel)),
                    new CommandContextItem(new YouTubeChannelVideosPage(channel.ChannelId, channel.Name)),
                    new CommandContextItem(new YouTubeAPIPage()),
                ],
        }).ToArray();

        return section;
    }

    // Method to fetch channels from YouTube API
    private static async Task<List<YouTubeChannel>> GetYouTubeChannels(string query)
    {
        var state = File.ReadAllText(YouTubeHelper.StateJsonPath());
        var jsonState = JsonNode.Parse(state);
        var apiKey = jsonState["apiKey"]?.ToString() ?? string.Empty;

        var channels = new List<YouTubeChannel>();

        using (var client = new HttpClient())
        {
            try
            {
                // Send the request to the YouTube API with the provided query to search for channels
                var response = await client.GetStringAsync($"https://www.googleapis.com/youtube/v3/search?part=snippet&type=channel&q={query}&key={apiKey}&maxResults=20");
                var json = JsonNode.Parse(response);

                // Parse the response
                if (json?["items"] is JsonArray itemsArray)
                {
                    foreach (var item in itemsArray)
                    {
                        // Add each channel to the list with channel details
                        channels.Add(new YouTubeChannel
                        {
                            Name = item["snippet"]?["channelTitle"]?.ToString() ?? string.Empty,
                            ChannelId = item["snippet"]?["channelId"]?.ToString() ?? string.Empty,
                            ChannelUrl = $"https://www.youtube.com/channel/{item["snippet"]?["channelId"]?.ToString()}" ?? string.Empty,
                            ProfilePicUrl = item["snippet"]?["thumbnails"]?["default"]?["url"]?.ToString() ?? string.Empty, // Get the default profile picture
                            Description = item["snippet"]?["description"]?.ToString() ?? string.Empty,
                            SubscriberCount = await GetChannelSubscriberCount(apiKey, item["snippet"]?["channelId"]?.ToString()), // Fetch the subscriber count
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                // Handle any errors from the API call or parsing
                channels.Add(new YouTubeChannel
                {
                    Name = "Error fetching data",
                    Description = $"Error: {ex.Message}",
                });
            }
        }

        return channels;
    }

    // Fetch subscriber count for each channel using a separate API call
    private static async Task<long> GetChannelSubscriberCount(string apiKey, string channelId)
    {
        using var client = new HttpClient();
        {
            try
            {
                var response = await client.GetStringAsync($"https://www.googleapis.com/youtube/v3/channels?part=statistics&id={channelId}&key={apiKey}");
                var json = JsonNode.Parse(response);

                if (json?["items"] is JsonArray itemsArray && itemsArray.Count > 0)
                {
                    var statistics = itemsArray[0]?["statistics"];
                    if (long.TryParse(statistics?["subscriberCount"]?.ToString(), out var subscriberCount))
                    {
                        return subscriberCount;
                    }
                }
            }
            catch
            {
                // In case of any error, return 0 subscribers
                return 0;
            }
        }

        return 0;
    }
}
