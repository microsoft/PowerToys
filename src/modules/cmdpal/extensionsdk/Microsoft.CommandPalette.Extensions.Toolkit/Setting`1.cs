// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Microsoft.CommandPalette.Extensions.Toolkit;

public abstract class Setting<T> : ISettingsForm
{
    private static readonly JsonSerializerOptions _jsonSerializerOptions = new() { WriteIndented = true };

    public T? Value { get; set; }

    public string Key { get; }

    public bool IsRequired { get; set; }

    public string ErrorMessage { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    protected Setting()
    {
        Value = default;
        Key = string.Empty;
    }

    public Setting(string key, T defaultValue)
    {
        Key = key;
        Value = defaultValue;
    }

    public Setting(string key, string label, string description, T defaultValue)
    {
        Key = key;
        Value = defaultValue;
        Label = label;
        Description = description;
    }

    public abstract Dictionary<string, object> ToDictionary();

    public string ToDataIdentifier() => $"\"{Key}\": \"{Key}\"";

    public string ToForm()
    {
        var controlElementNode = JsonNode.Parse(JsonSerializer.Serialize(ToDictionary(), JsonSerializationContext.Default.Dictionary)) as JsonObject ?? new JsonObject();

        var labelText = Label;
        var descriptionText = Description;

        if (controlElementNode.TryGetPropertyValue("label", out var existingLabelNode) &&
            string.IsNullOrWhiteSpace(labelText) &&
            existingLabelNode is not null)
        {
            labelText = existingLabelNode.GetValue<string>();
        }

        if (controlElementNode.TryGetPropertyValue("title", out var existingTitleNode) &&
            string.IsNullOrWhiteSpace(labelText) &&
            existingTitleNode is not null)
        {
            labelText = existingTitleNode.GetValue<string>();
        }

        if (controlElementNode.TryGetPropertyValue("label", out var controlDescriptionNode) &&
            string.IsNullOrWhiteSpace(descriptionText) &&
            controlDescriptionNode is not null)
        {
            descriptionText = controlDescriptionNode.GetValue<string>();
        }

        if (!string.IsNullOrWhiteSpace(labelText))
        {
            controlElementNode["label"] = labelText;
            controlElementNode["labelPosition"] = "hidden";
        }

        var labelElements = new JsonArray();
        if (!string.IsNullOrWhiteSpace(labelText))
        {
            labelElements.Add(new JsonObject
            {
                ["type"] = "TextBlock",
                ["text"] = labelText,
                ["wrap"] = true,
                ["spacing"] = "None"
            });
        }

        if (!string.IsNullOrWhiteSpace(descriptionText))
        {
            var descriptionBlock = new JsonObject
            {
                ["type"] = "TextBlock",
                ["text"] = descriptionText,
                ["weight"] = "Bolder",
                ["wrap"] = true
            };

            descriptionBlock["spacing"] = labelElements.Count > 0 ? "Small" : "None";
            labelElements.Add(descriptionBlock);
        }

        var bodyElements = new JsonArray();

        if (controlElementNode.TryGetPropertyValue("type", out var typeNode) &&
            typeNode is not null &&
            string.Equals(typeNode.GetValue<string>(), "Input.Toggle", StringComparison.OrdinalIgnoreCase))
        {
            controlElementNode["title"] = string.Empty;

            if (labelElements.Count == 0)
            {
                bodyElements.Add(controlElementNode);
            }
            else
            {
                var columnSet = new JsonObject
                {
                    ["type"] = "ColumnSet"
                };

                columnSet["columns"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["type"] = "Column",
                        ["width"] = "auto",
                        ["verticalContentAlignment"] = "Top",
                        ["items"] = new JsonArray(controlElementNode)
                    },
                    new JsonObject
                    {
                        ["type"] = "Column",
                        ["width"] = "stretch",
                        ["items"] = labelElements
                    }
                };

                bodyElements.Add(columnSet);
            }
        }
        else
        {
            foreach (var element in labelElements)
            {
                if (element is not null)
                {
                    bodyElements.Add(element.DeepClone());
                }
            }

            bodyElements.Add(controlElementNode);
        }

        var card = new JsonObject
        {
            ["$schema"] = "http://adaptivecards.io/schemas/adaptive-card.json",
            ["type"] = "AdaptiveCard",
            ["version"] = "1.5",
            ["body"] = bodyElements,
            ["actions"] = new JsonArray
            {
                new JsonObject
                {
                    ["type"] = "Action.Submit",
                    ["title"] = "Save",
                    ["data"] = new JsonObject
                    {
                        [Key] = Key
                    }
                }
            }
        };

        return card.ToJsonString(_jsonSerializerOptions);
    }

    public abstract void Update(JsonObject payload);

    public abstract string ToState();
}
