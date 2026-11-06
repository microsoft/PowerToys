// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Text.Json;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace CompatibilityExtension;

internal sealed partial class CompatibilityForm : FormContent
{
    private readonly Action<string> _showResult;

    public CompatibilityForm(Action<string> showResult)
    {
        _showResult = showResult;
        TemplateJson = """
            {
              "$schema": "http://adaptivecards.io/schemas/adaptive-card.json",
              "type": "AdaptiveCard",
              "version": "1.5",
              "body": [
                { "type": "TextBlock", "text": "Compatibility form", "weight": "Bolder", "size": "Medium" },
                { "type": "Input.Text", "id": "name", "label": "Name", "value": "Ada" },
                { "type": "Input.ChoiceSet", "id": "color", "label": "Color", "value": "blue",
                  "choices": [ { "title": "Blue", "value": "blue" }, { "title": "Green", "value": "green" } ] },
                { "type": "Input.Toggle", "id": "enabled", "title": "Enabled", "value": "true", "valueOn": "true", "valueOff": "false" }
              ],
              "actions": [ { "type": "Action.Submit", "title": "Submit" } ]
            }
            """;
    }

    public override ICommandResult SubmitForm(string inputs)
    {
        try
        {
            using var document = JsonDocument.Parse(inputs);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return CommandResult.ShowToast("Expected form input values");
            }

            _showResult($"Submitted: {ReadString(root, "name")}; color: {ReadString(root, "color")}; enabled: {ReadString(root, "enabled")}");
            return CommandResult.KeepOpen();
        }
        catch (JsonException)
        {
            return CommandResult.ShowToast("Invalid form input");
        }
    }

    private static string ReadString(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : string.Empty;
}
