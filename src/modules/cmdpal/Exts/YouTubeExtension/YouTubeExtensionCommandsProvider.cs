// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.UI.ApplicationSettings;
using YouTubeExtension.Helper;
using YouTubeExtension.Pages;

namespace YouTubeExtension;

public partial class YouTubeExtensionCommandsProvider : CommandProvider
{
    public YouTubeExtensionCommandsProvider()
    {
        DisplayName = "YouTube";
    }

    private readonly ICommandItem[] _commands = [
        new CommandItem(new YouTubeVideosPage())
            {
                Title = "Search Videos on YouTube",
                Subtitle = "YouTube",
            },
        new CommandItem(new YouTubeChannelsPage())
            {
                Title = "Search Channels on YouTube",
                Subtitle = "YouTube",
            },
    ];

    private readonly YouTubeAPIPage apiPage = new();

    public override ICommandItem[] TopLevelCommands()
    {
        return TopLevelCommandsAsync().GetAwaiter().GetResult();
    }

    public async Task<ICommandItem[]> TopLevelCommandsAsync()
    {
        var settingsPath = YouTubeHelper.StateJsonPath();

        // Check if the settings file exists
        if (!File.Exists(settingsPath))
        {
            return new[]
            {
                new CommandItem(apiPage)
                    {
                        Title = "YouTube Extension",
                        Subtitle = "Enter your API key.",
                    },
            };
        }

        // Read the file and parse the API key
        var state = File.ReadAllText(settingsPath);
        var jsonState = System.Text.Json.Nodes.JsonNode.Parse(state);
        var apiKey = jsonState?["apiKey"]?.ToString() ?? string.Empty;

        // Validate the API key using YouTube API
        if (string.IsNullOrWhiteSpace(apiKey) || !await IsApiKeyValid(apiKey))
        {
            return new[]
            {
                new CommandItem(apiPage)
                    {
                        Title = "YouTube Extension",
                        Subtitle = "Enter your API key.",
                    },
            };
        }

        // If file exists and API key is valid, return commands
        return _commands;
    }

    // Method to check if the API key is valid by making a simple request to the YouTube API
    private static async Task<bool> IsApiKeyValid(string apiKey)
    {
        using HttpClient client = new HttpClient();
        {
            try
            {
                // Make a simple request to verify the API key, such as fetching a video
                var response = await client.GetAsync($"https://www.googleapis.com/youtube/v3/search?part=snippet&type=video&q=test&key={apiKey}");

                // If the response status code is 200, the API key is valid
                if (response.IsSuccessStatusCode)
                {
                    return true;
                }

                // Optionally, handle other status codes and log errors
                return false;
            }
            catch
            {
                // If any exception occurs (e.g., network error), consider the API key invalid
                return false;
            }
        }
    }
}
