// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO;
using System.Text.Json.Nodes;
using Microsoft.CommandPalette.Extensions.Toolkit;
using YouTubeExtension.Helper;

namespace YouTubeExtension.Pages;

internal sealed partial class YouTubeAPIForm : FormContent
{
    public YouTubeAPIForm()
    {
        TemplateJson = $$"""
                    {
                      "$schema": "http://adaptivecards.io/schemas/adaptive-card.json",
                      "type": "AdaptiveCard",
                      "version": "1.5",
                      "body": [
                        {
                          "type": "Input.Text",
                          "style": "password",
                          "id": "apiKey",
                          "label": "API Key",
                          "isRequired": true,
                          "errorMessage": "API Key required"
                        }
                      ],
                      "actions": [
                        {
                          "type": "Action.Submit",
                          "title": "Save",
                          "data": {
                            "apiKey": "apiKey"
                          }
                        }
                      ]
                    }
                    """;
    }

    public override CommandResult SubmitForm(string payload)
    {
        var formInput = JsonNode.Parse(payload);
        if (formInput == null)
        {
            return CommandResult.GoHome();
        }

        // get the name and url out of the values
        var formApiKey = formInput["apiKey"] ?? string.Empty;

        // Construct a new json blob with the name and url
        var json = $$"""
                    {
                        "apiKey": "{{formApiKey}}"
                    }
                    """;

        File.WriteAllText(YouTubeHelper.StateJsonPath(), json);
        return CommandResult.GoHome();
    }
}
