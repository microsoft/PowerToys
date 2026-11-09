// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Text;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace SamplePagesExtension;

internal sealed partial class SampleMediaPlayerPage : ListPage
{
    internal const string CommandId = "com.microsoft.cmdpal.samples.media";

    public SampleMediaPlayerPage()
    {
        Id = CommandId;
        Name = "Media player";
        Title = "Sample media player";
        Icon = new IconInfo("\uE8D6");
    }

    public override IListItem[] GetItems() =>
    [
        new ListItem(new NoOpCommand()) { Title = "Previous track", Icon = new IconInfo("\uE892") },
        new ListItem(new NoOpCommand()) { Title = "Play or pause", Icon = new IconInfo("\uE768") },
        new ListItem(new NoOpCommand()) { Title = "Next track", Icon = new IconInfo("\uE893") },
        new ListItem(new NoOpCommand()) { Title = "Breaking The Habit", Subtitle = "Linkin Park - Best of 2000s" },
    ];

    internal static FormContent CreateSmallContent()
    {
        var background = Svg("""
            <svg xmlns="http://www.w3.org/2000/svg" width="360" height="100">
              <defs><linearGradient id="background"><stop stop-color="#293b52"/><stop offset="1" stop-color="#10141e"/></linearGradient></defs>
              <rect width="360" height="100" fill="url(#background)"/>
              <circle cx="280" cy="50" r="95" fill="#ffffff" opacity=".04"/>
              <circle cx="40" cy="150" r="110" fill="#ffffff" opacity=".04"/>
            </svg>
            """);
        var previous = Svg("""<svg xmlns="http://www.w3.org/2000/svg" width="30" height="30"><path fill="white" d="M5 7h3v16H5zm19 0v16L9 15z"/></svg>""");
        var play = Svg("""<svg xmlns="http://www.w3.org/2000/svg" width="54" height="54"><circle cx="27" cy="27" r="25" fill="white"/><path fill="#293b52" d="m22 15 18 12-18 12z"/></svg>""");
        var next = Svg("""<svg xmlns="http://www.w3.org/2000/svg" width="30" height="30"><path fill="white" d="M22 7h3v16h-3zM6 7l15 8-15 8z"/></svg>""");

        return new FormContent
        {
            TemplateJson = $$"""
                {
                  "$schema": "https://adaptivecards.io/schemas/adaptive-card.json",
                  "type": "AdaptiveCard",
                  "version": "1.5",
                  "minHeight": "100px",
                  "backgroundImage": { "url": "{{background}}", "fillMode": "cover" },
                  "body": [
                    {
                      "type": "ColumnSet", "horizontalAlignment": "center",
                      "columns": [
                        { "type": "Column", "width": "stretch", "items": [] },
                        { "type": "Column", "width": "auto", "verticalContentAlignment": "center",
                          "items": [{ "type": "Image", "url": "{{previous}}", "altText": "Previous track", "width": "30px", "height": "30px",
                            "selectAction": { "type": "Action.Execute", "id": "media.previous", "verb": "media.previous", "data": { "command": "previous" } } }] },
                        { "type": "Column", "width": "auto", "spacing": "medium",
                          "items": [{ "type": "Image", "url": "{{play}}", "altText": "Play or pause", "width": "40px", "height": "40px",
                            "selectAction": { "type": "Action.Execute", "id": "media.playPause", "verb": "media.playPause", "data": { "command": "playPause" } } }] },
                        { "type": "Column", "width": "auto", "spacing": "medium", "verticalContentAlignment": "center",
                          "items": [{ "type": "Image", "url": "{{next}}", "altText": "Next track", "width": "30px", "height": "30px",
                            "selectAction": { "type": "Action.Execute", "id": "media.next", "verb": "media.next", "data": { "command": "next" } } }] },
                        { "type": "Column", "width": "stretch", "items": [] }
                      ]
                    },
                    { "type": "RichTextBlock", "spacing": "small", "inlines": [
                      { "type": "TextRun", "text": "Linkin Park  ", "color": "light" },
                      { "type": "TextRun", "text": "Breaking The Habit", "weight": "bolder", "color": "light" }
                    ] },
                    { "type": "TextBlock", "text": "Best of 2000s", "color": "light", "isSubtle": true, "spacing": "none" }
                  ]
                }
                """,
        };
    }

    private static string Svg(string svg) => "data:image/svg+xml;base64," + Convert.ToBase64String(Encoding.UTF8.GetBytes(svg));
}
