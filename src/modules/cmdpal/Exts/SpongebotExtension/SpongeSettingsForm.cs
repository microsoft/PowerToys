// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Text.Json.Nodes;
using Microsoft.CmdPal.Extensions;
using Microsoft.CmdPal.Extensions.Helpers;

namespace SpongebotExtension;

internal sealed partial class SpongeSettingsForm : Form
{
    public override string TemplateJson()
    {
        var json = $$"""
{
  "$schema": "http://adaptivecards.io/schemas/adaptive-card.json",
  "type": "AdaptiveCard",
  "version": "1.5",
  "body": [
    {
      "type": "Input.Text",
      "style": "text",
      "id": "username",
      "label": "Username",
      "isRequired": true,
      "errorMessage": "Username is required"
    },
    {
      "type": "Input.Text",
      "style": "password",
      "id": "password",
      "label": "Password",
      "isRequired": true,
      "errorMessage": "Password is required"
    }
  ],
  "actions": [
    {
      "type": "Action.Submit",
      "title": "Save",
      "data": {
        "username": "username",
        "password": "password"
      }
    }
  ]
}
""";
        return json;
    }

    public override CommandResult SubmitForm(string payload)
    {
        var formInput = JsonNode.Parse(payload);
        if (formInput == null)
        {
            return CommandResult.GoHome();
        }

        // get the name and url out of the values
        var formName = formInput["username"] ?? string.Empty;
        var formPassword = formInput["password"] ?? string.Empty;

        // Construct a new json blob with the name and url
        var json = $$"""
{
    "username": "{{formName}}",
    "password": "{{formPassword}}"
}
""";

        File.WriteAllText(SpongebotPage.StateJsonPath(), json);
        return CommandResult.GoHome();
    }
}
