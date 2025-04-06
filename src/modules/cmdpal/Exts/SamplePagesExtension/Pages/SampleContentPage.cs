// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Nodes;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace SamplePagesExtension;

internal sealed partial class SampleContentPage : ContentPage
{
    private readonly SampleContentForm sampleForm = new();
    private readonly MarkdownContent sampleMarkdown = new() { Body = "# Sample page with mixed content \n This page has both markdown, and form content" };

    public override IContent[] GetContent() => [sampleMarkdown, sampleForm];

    public SampleContentPage()
    {
        Name = "Open";
        Title = "Sample Content";
        Icon = new IconInfo("\uECA5"); // Tiles

        Commands = [
            new CommandContextItem(
                title: "Do thing",
                name: "Do thing",
                subtitle: "Pops a toast",
                result: CommandResult.ShowToast(new ToastArgs() { Message = "what's up doc", Result = CommandResult.KeepOpen() }),
                action: () => { Title = Title + "+1"; }),
            new CommandContextItem(
                title: "Something else",
                name: "Something else",
                subtitle: "Something else",
                result: CommandResult.ShowToast(new ToastArgs() { Message = "turn down for what?", Result = CommandResult.KeepOpen() }),
                action: () => { Title = Title + "-1"; }),
        ];
    }
}

[SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1402:File may only contain a single type", Justification = "Sample code")]
internal sealed partial class SampleContentForm : FormContent
{
    public SampleContentForm()
    {
        TemplateJson = $$"""
{
    "$schema": "http://adaptivecards.io/schemas/adaptive-card.json",
    "type": "AdaptiveCard",
    "version": "1.6",
    "body": [
        {
            "type": "TextBlock",
            "size": "medium",
            "weight": "bolder",
            "text": " ${ParticipantInfoForm.title}",
            "horizontalAlignment": "center",
            "wrap": true,
            "style": "heading"
        },
        {
            "type": "Input.Text",
            "label": "Name",
            "style": "text",
            "id": "SimpleVal",
            "isRequired": true,
            "errorMessage": "Name is required",
            "placeholder": "Enter your name"
        },
        {
            "type": "Input.Text",
            "label": "Homepage",
            "style": "url",
            "id": "UrlVal",
            "placeholder": "Enter your homepage url"
        },
        {
            "type": "Input.Text",
            "label": "Email",
            "style": "email",
            "id": "EmailVal",
            "placeholder": "Enter your email"
        },
        {
            "type": "Input.Text",
            "label": "Phone",
            "style": "tel",
            "id": "TelVal",
            "placeholder": "Enter your phone number"
        },
        {
            "type": "Input.Text",
            "label": "Comments",
            "style": "text",
            "isMultiline": true,
            "id": "MultiLineVal",
            "placeholder": "Enter any comments"
        },
        {
            "type": "Input.Number",
            "label": "Quantity (Minimum -5, Maximum 5)",
            "min": -5,
            "max": 5,
            "value": 1,
            "id": "NumVal",
            "errorMessage": "The quantity must be between -5 and 5"
        },
        {
            "type": "Input.Date",
            "label": "Due Date",
            "id": "DateVal",
            "value": "2017-09-20"
        },
        {
            "type": "Input.Time",
            "label": "Start time",
            "id": "TimeVal",
            "value": "16:59"
        },
        {
            "type": "TextBlock",
            "size": "medium",
            "weight": "bolder",
            "text": "${Survey.title} ",
            "horizontalAlignment": "center",
            "wrap": true,
            "style": "heading"
        },
        {
            "type": "Input.ChoiceSet",
            "id": "CompactSelectVal",
            "label": "${Survey.questions[0].question}",
            "style": "compact",
            "value": "1",
            "choices": [
                {
                    "$data": "${Survey.questions[0].items}",
                    "title": "${choice}",
                    "value": "${value}"
                }
            ]
        },
        {
            "type": "Input.ChoiceSet",
            "id": "SingleSelectVal",
            "label": "${Survey.questions[1].question}",
            "style": "expanded",
            "value": "1",
            "choices": [
                {
                    "$data": "${Survey.questions[1].items}",
                    "title": "${choice}",
                    "value": "${value}"
                }
            ]
        },
        {
            "type": "Input.ChoiceSet",
            "id": "MultiSelectVal",
            "label": "${Survey.questions[2].question}",
            "isMultiSelect": true,
            "value": "1,3",
            "choices": [
                {
                    "$data": "${Survey.questions[2].items}",
                    "title": "${choice}",
                    "value": "${value}"
                }
            ]
        },
        {
            "type": "TextBlock",
            "size": "medium",
            "weight": "bolder",
            "text": "Input.Toggle",
            "horizontalAlignment": "center",
            "wrap": true,
            "style": "heading"
        },
        {
            "type": "Input.Toggle",
            "label": "Please accept the terms and conditions:",
            "title": "${Survey.questions[3].question}",
            "valueOn": "true",
            "valueOff": "false",
            "id": "AcceptsTerms",
            "isRequired": true,
            "errorMessage": "Accepting the terms and conditions is required"
        },
        {
            "type": "Input.Toggle",
            "label": "How do you feel about red cars?",
            "title": "${Survey.questions[4].question}",
            "valueOn": "RedCars",
            "valueOff": "NotRedCars",
            "id": "ColorPreference"
        }
    ],
    "actions": [
        {
            "type": "Action.Submit",
            "title": "Submit",
            "data": {
                "id": "1234567890"
            }
        },
        {
            "type": "Action.ShowCard",
            "title": "Show Card",
            "card": {
                "type": "AdaptiveCard",
                "body": [
                    {
                        "type": "Input.Text",
                        "label": "Enter comment",
                        "style": "text",
                        "id": "CommentVal"
                    }
                ],
                "actions": [
                    {
                        "type": "Action.Submit",
                        "title": "OK"
                    }
                ]
            }
        }
    ]
}
""";

        DataJson = $$"""
{
    "ParticipantInfoForm": {
        "title": "Input.Text elements"
    },
    "Survey": {
        "title": "Input ChoiceSet",
        "questions": [
            {
                "question": "What color do you want? (compact)",
                "items": [
                    {
                        "choice": "Red",
                        "value": "1"
                    },
                    {
                        "choice": "Green",
                        "value": "2"
                    },
                    {
                        "choice": "Blue",
                        "value": "3"
                    }
                ]
            },
            {
                "question": "What color do you want? (expanded)",
                "items": [
                    {
                        "choice": "Red",
                        "value": "1"
                    },
                    {
                        "choice": "Green",
                        "value": "2"
                    },
                    {
                        "choice": "Blue",
                        "value": "3"
                    }
                ]
            },
            {
                "question": "What color do you want? (multiselect)",
                "items": [
                    {
                        "choice": "Red",
                        "value": "1"
                    },
                    {
                        "choice": "Green",
                        "value": "2"
                    },
                    {
                        "choice": "Blue",
                        "value": "3"
                    }
                ]
            },
            {
                "question": "I accept the terms and conditions (True/False)"
            },
            {
                "question": "Red cars are better than other cars"
            }
        ]
    }
}
""";
    }

    public override CommandResult SubmitForm(string payload)
    {
        var formInput = JsonNode.Parse(payload)?.AsObject();
        if (formInput == null)
        {
            return CommandResult.GoHome();
        }

        // Application.Current.GetService<ILocalSettingsService>().SaveSettingAsync("GlobalHotkey", formInput["hotkey"]?.ToString() ?? string.Empty);
        return CommandResult.GoHome();
    }
}

[SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1402:File may only contain a single type", Justification = "Sample code")]
internal sealed partial class SampleTreeContentPage : ContentPage
{
    private readonly TreeContent myContentTree;

    public override IContent[] GetContent() => [myContentTree];

    public SampleTreeContentPage()
    {
        Name = Title = "Sample Content";
        Icon = new IconInfo("\uE81E");

        myContentTree = new()
        {
            RootContent = new MarkdownContent() { Body = "# This page has nested content" },
            Children = [
                new TreeContent()
                {
                    RootContent = new MarkdownContent() { Body = "Yo dog" },
                    Children = [
                        new TreeContent()
                        {
                            RootContent = new MarkdownContent() { Body = "I heard you like content" },
                            Children = [
                                new MarkdownContent() { Body = "So we put content in your content" },
                                new FormContent() { TemplateJson = "{\"$schema\":\"http://adaptivecards.io/schemas/adaptive-card.json\",\"type\":\"AdaptiveCard\",\"version\":\"1.6\",\"body\":[{\"type\":\"TextBlock\",\"size\":\"medium\",\"weight\":\"bolder\",\"text\":\"Mix and match why don't you\",\"horizontalAlignment\":\"center\",\"wrap\":true,\"style\":\"heading\"},{\"type\":\"TextBlock\",\"text\":\"You can have forms here too\",\"horizontalAlignment\":\"Right\",\"wrap\":true}],\"actions\":[{\"type\":\"Action.Submit\",\"title\":\"It's a form, you get it\",\"data\":{\"id\":\"LoginVal\"}}]}" },
                                new MarkdownContent() { Body = "Another markdown down here" },
                            ],
                        },
                        new MarkdownContent() { Body = "**slaps roof**" },
                        new MarkdownContent() { Body = "This baby can fit so much content" },

                    ],
                }
            ],
        };
    }
}
