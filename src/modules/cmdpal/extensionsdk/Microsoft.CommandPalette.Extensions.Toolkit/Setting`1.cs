// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

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
        var controlElement = ToDictionary();

        var labelText = Label;
        var descriptionText = Description;

        if (controlElement.TryGetValue("label", out var existingLabel) && string.IsNullOrWhiteSpace(labelText) && existingLabel is string existingLabelText)
        {
            labelText = existingLabelText;
        }

        if (controlElement.TryGetValue("title", out var existingTitle) && string.IsNullOrWhiteSpace(labelText) && existingTitle is string existingTitleText)
        {
            labelText = existingTitleText;
        }

        if (controlElement.TryGetValue("label", out var controlDescription) && string.IsNullOrWhiteSpace(descriptionText) && controlDescription is string descriptionFromControl)
        {
            descriptionText = descriptionFromControl;
        }

        if (!string.IsNullOrWhiteSpace(labelText))
        {
            controlElement["label"] = labelText;
            controlElement["labelPosition"] = "hidden";
        }

        var labelElements = new List<Dictionary<string, object>>();
        if (!string.IsNullOrWhiteSpace(labelText))
        {
            labelElements.Add(new()
            {
                { "type", "TextBlock" },
                { "text", labelText },
                { "weight", "Bolder" },
                { "wrap", true }
            });
        }

        if (!string.IsNullOrWhiteSpace(descriptionText))
        {
            labelElements.Add(new()
            {
                { "type", "TextBlock" },
                { "text", descriptionText },
                { "isSubtle", true },
                { "wrap", true },
                { "spacing", "None" }
            });
        }

        var bodyElements = new List<object>();

        if (controlElement.TryGetValue("type", out var typeValue) && typeValue is string typeString && typeString.Equals("Input.Toggle", StringComparison.OrdinalIgnoreCase))
        {
            controlElement["title"] = string.Empty;

            if (labelElements.Count == 0)
            {
                bodyElements.Add(controlElement);
            }
            else
            {
                bodyElements.Add(new Dictionary<string, object>
                {
                    { "type", "ColumnSet" },
                    { "columns", new List<object>
                        {
                            new Dictionary<string, object>
                            {
                                { "type", "Column" },
                                { "width", "auto" },
                                { "verticalContentAlignment", "Center" },
                                { "items", new List<object> { controlElement } }
                            },
                            new Dictionary<string, object>
                            {
                                { "type", "Column" },
                                { "width", "stretch" },
                                { "items", labelElements }
                            }
                        }
                    }
                });
            }
        }
        else
        {
            bodyElements.AddRange(labelElements);
            bodyElements.Add(controlElement);
        }

        var card = new Dictionary<string, object>
        {
            { "$schema", "http://adaptivecards.io/schemas/adaptive-card.json" },
            { "type", "AdaptiveCard" },
            { "version", "1.5" },
            { "body", bodyElements },
            {
                "actions",
                new List<object>
                {
                    new Dictionary<string, object>
                    {
                        { "type", "Action.Submit" },
                        { "title", "Save" },
                        {
                            "data",
                            new Dictionary<string, object>
                            {
                                { Key, Key }
                            }
                        }
                    }
                }
            }
        };

        return JsonSerializer.Serialize(card, _jsonSerializerOptions);
    }

    public abstract void Update(JsonObject payload);

    public abstract string ToState();
}
