// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Text.Json.Nodes;
using Microsoft.CmdPal.Extensions;
using Microsoft.CmdPal.Extensions.Helpers;

namespace SamplePagesExtension;

internal sealed class SampleForm : Form
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
      "id": "text1",
      "label": "Input.Text",
      "isRequired": true,
      "errorMessage": "Text is required"
    }
  ],
  "actions": [
    {
      "type": "Action.Submit",
      "title": "Action.Submit",
      "data": {
        "text1": "text1",
      }
    }
  ]
}
""";
        return json;
    }

    public override string DataJson() => throw new NotImplementedException();

    public override string StateJson() => throw new NotImplementedException();

    public override CommandResult SubmitForm(string payload)
    {
        var formInput = JsonNode.Parse(payload);
        if (formInput == null)
        {
            return CommandResult.GoHome();
        }

        // get the name and url out of the values

        // var testInput = formInput["test1"] ?? string.Empty;
        return CommandResult.GoHome();
    }
}
