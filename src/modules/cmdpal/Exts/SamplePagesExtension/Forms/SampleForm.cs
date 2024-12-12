// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Text.Json.Nodes;
using Microsoft.CmdPal.Extensions.Helpers;

namespace SamplePagesExtension;

internal sealed partial class SampleForm : Form
{
    public SampleForm()
    {
    }

    public override string TemplateJson()
    {
        var json = $$"""
{
    "$schema": "http://adaptivecards.io/schemas/adaptive-card.json",
    "type": "AdaptiveCard",
    "version": "1.5",
    "body": [
        {
          "type": "TextBlock",
          "text": "🚧 This is a sample form 🚧",
          "weight": "bolder",
          "size": "extraLarge",
          "spacing": "none",
          "wrap": true,
          "style": "heading"
        },
        {
            "type": "Input.Text",
            "style": "text",
            "id": "hotkey",
            "label": "Input.Text Example",
            "value": "example value",
            "isRequired": false
        }
    ],
    "actions": [
        {
            "type": "Action.Submit",
            "title": "Save",
            "data": {
                "name": "name",
                "url": "url"
            }
        }
    ]
}
""";
        return json;
    }

    public override string StateJson() => "{}";

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
