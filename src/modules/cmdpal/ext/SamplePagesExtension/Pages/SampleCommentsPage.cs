// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace SamplePagesExtension;

internal sealed partial class SampleCommentsPage : ContentPage
{
    private readonly TreeContent myContentTree;

    public override IContent[] GetContent() => [myContentTree];

    public SampleCommentsPage()
    {
        Name = "View Posts";
        Icon = new IconInfo("\uE90A"); // Comment

        myContentTree = new()
        {
            RootContent = new MarkdownContent()
            {
                Body = """
# Example of a thread of comments
You can use TreeContent in combination with FormContent to build a structure like a page with comments.

The forms on this page use the AdaptiveCard `Action.ShowCard` action to show a nested, hidden card on the form.
""",
            },

            Children = [
                new PostContent("First")
                {
                    Replies = [
                        new PostContent("Oh very insightful. I hadn't considered that"),
                        new PostContent("Second"),
                        new PostContent("ah the ol switcheroo"),
                    ],
                },
                new PostContent("First\nEDIT: shoot")
                {
                    Replies = [
                        new PostContent("delete this"),
                    ],
                },
                new PostContent("Do you think they get the picture")
                {
                    Replies = [
                        new PostContent("Probably! Now go build and be happy"),
                    ],
                }
            ],
        };
    }
}

[SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1402:File may only contain a single type", Justification = "Sample code")]
internal sealed partial class PostContent : TreeContent
{
    public List<IContent> Replies { get; init; } = [];

    private readonly ToastStatusMessage _toast = new(new StatusMessage() { Message = "Reply posted", State = MessageState.Success });

    public PostContent(string body)
    {
        RootContent = new PostForm(body, this);
    }

    public override IContent[] GetChildren() => Replies.ToArray();

    public void Post()
    {
        RaiseItemsChanged(Replies.Count);
        _toast.Show();
    }
}

[SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1402:File may only contain a single type", Justification = "Sample code")]
internal sealed partial class PostForm : FormContent
{
    private readonly PostContent _parent;

    public PostForm(string postBody, PostContent parent)
    {
        _parent = parent;
        TemplateJson = """
{
    "$schema": "http://adaptivecards.io/schemas/adaptive-card.json",
    "type": "AdaptiveCard",
    "version": "1.6",
    "body": [
        {
            "type": "TextBlock",
            "text": "${postBody}",
            "wrap": true
        }
    ],
    "actions": [
        {
            "type": "Action.ShowCard",
            "title": "${replyCard.title}",
            "card": {
                "type": "AdaptiveCard",
                "$schema": "http://adaptivecards.io/schemas/adaptive-card.json",
                "version": "1.6",
                "body": [
                    {
                        "type": "Container",
                        "id": "${replyCard.idPrefix}Properties",
                        "items": [
                            {
                                "$data": "${replyCard.fields}",
                                "type": "Input.Text",
                                "label": "${label}",
                                "id": "${id}",
                                "isRequired": "${required}",
                                "isMultiline": true,
                                "errorMessage": "'${label}' is required"
                            }
                        ]
                    }
                ],
                "actions": [
                    {
                        "type": "Action.Submit",
                        "title": "Post"
                    }
                ]
            }
        },
        {
            "type": "Action.Submit",
            "title": "Favorite"
        },
        {
            "type": "Action.Submit",
            "title": "View on web"
        }
    ]
}
""";
        DataJson = $$"""
{
    "postBody": {{JsonSerializer.Serialize(postBody, JsonSerializationContext.Default.String)}},
    "replyCard": {
        "title": "Reply",
        "idPrefix": "reply",
        "fields": [
            {
                "label": "Reply",
                "id": "ReplyBody",
                "required": true,
                "placeholder": "Write a reply here"
            }
        ]
    }
}
""";
    }

    public override ICommandResult SubmitForm(string payload)
    {
        var data = JsonNode.Parse(payload);
        _ = data;
        var reply = data["ReplyBody"];
        var s = reply?.AsValue()?.ToString();
        if (!string.IsNullOrEmpty(s))
        {
            _parent.Replies.Add(new PostContent(s));
            _parent.Post();
        }

        return CommandResult.KeepOpen();
    }
}

[JsonSerializable(typeof(float))]
[JsonSerializable(typeof(int))]
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(bool))]
[JsonSourceGenerationOptions(UseStringEnumConverter = true, WriteIndented = true)]
[System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1402:File may only contain a single type", Justification = "Just used here")]
internal sealed partial class JsonSerializationContext : JsonSerializerContext
{
}
