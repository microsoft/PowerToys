// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace SamplePagesExtension;

/// <summary>
/// A sample dock band demonstrating the Live Tile flip effect.
/// Each item shows primary content on the front and flips to reveal
/// secondary content on the back, cycling at a configurable interval.
/// </summary>
internal sealed partial class SampleLiveTileDockBand : WrappedDockItem
{
    public SampleLiveTileDockBand()
        : base([], "com.microsoft.cmdpal.samples.livetile_band", "Sample Live Tiles")
    {
        ListItem[] items =
        [
            new(new ShowToastCommand("Weather"))
            {
                Title = "Weather",
                Subtitle = "Seattle, WA",
                Icon = new IconInfo("\uE9CA"), // Cloud
                LiveTileContent = new LiveTileContent(
                    title: "72\u00b0F",
                    subtitle: "Sunny",
                    icon: new IconInfo("\uE706"), // Sunny
                    flipIntervalMs: 5000),
            },
            new(new ShowToastCommand("Mail"))
            {
                Title = "Mail",
                Subtitle = "3 unread",
                Icon = new IconInfo("\uE715"), // Mail
                LiveTileContent = new LiveTileContent(
                    title: "From: Alice",
                    subtitle: "Check out this PR!",
                    icon: new IconInfo("\uE8C3"), // Read
                    flipIntervalMs: 7000),
            },
            new(new ShowToastCommand("Calendar"))
            {
                Title = "Next meeting",
                Subtitle = "in 30 min",
                Icon = new IconInfo("\uE787"), // Calendar
                LiveTileContent = new LiveTileContent(
                    title: "Sprint Review",
                    subtitle: "2:00 PM - Room 42",
                    icon: new IconInfo("\uE716"), // People
                    flipIntervalMs: 4000),
            },
        ];
        Icon = new IconInfo("\uF246"); // Tiles
        Items = items;
    }
}
