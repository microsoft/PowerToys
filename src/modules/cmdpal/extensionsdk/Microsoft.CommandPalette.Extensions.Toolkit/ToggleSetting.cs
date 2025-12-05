// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using System.Text.Json.Nodes;

namespace Microsoft.CommandPalette.Extensions.Toolkit;

public sealed class ToggleSetting : Setting<bool>
{
    private ToggleSetting()
        : base()
    {
    }

    public ToggleSetting(string key, bool defaultValue)
        : base(key, defaultValue)
    {
    }

    public ToggleSetting(string key, string label, string description, bool defaultValue)
        : base(key, label, description, defaultValue)
    {
    }

    public override Dictionary<string, object> ToDictionary()
    {
        var items = new List<Dictionary<string, object>>();

        if (!string.IsNullOrEmpty(Label))
        {
            items.Add(
                new()
                {
                    { "type", "TextBlock" },
                    { "text", Label },
                    { "wrap", true },
                });
        }

        if (!(string.IsNullOrEmpty(Description) || string.Equals(Description, Label, StringComparison.OrdinalIgnoreCase)))
        {
            items.Add(
                new()
                {
                    { "type", "TextBlock" },
                    { "text", Description },
                    { "isSubtle", true },
                    { "size", "Small" },
                    { "spacing", "Small" },
                    { "wrap", true },
                });
        }

        return new()
        {
            { "type", "ColumnSet" },
            {
                "columns", new List<Dictionary<string, object>>
                {
                    new()
                    {
                        { "type", "Column" },
                        { "width", "20px" },
                        {
                            "items", new List<Dictionary<string, object>>
                            {
                                new()
                                {
                                    { "type", "Input.Toggle" },
                                    { "title", " " },
                                    { "id", Key },
                                    { "value", JsonSerializer.Serialize(Value, JsonSerializationContext.Default.Boolean) },
                                    { "isRequired", IsRequired },
                                    { "errorMessage", ErrorMessage },
                                },
                            }
                        },
                        { "verticalContentAlignment", "Center" },
                    },
                    new()
                    {
                        { "type", "Column" },
                        { "width", "stretch" },
                        { "items", items },
                        { "verticalContentAlignment", "Center" },
                    },
                }
            },
            { "spacing", "Medium" },
        };
    }

    public static ToggleSetting LoadFromJson(JsonObject jsonObject) => new() { Value = jsonObject["value"]?.GetValue<bool>() ?? false };

    public override void Update(JsonObject payload)
    {
        // If the key doesn't exist in the payload, don't do anything
        if (payload[Key] is not null)
        {
            // Adaptive cards returns boolean values as a string "true"/"false", cause of course.
            var strFromJson = payload[Key]?.GetValue<string>() ?? string.Empty;
            var val = strFromJson switch { "true" => true, "false" => false, _ => false };
            Value = val;
        }
    }

    public override string ToState()
    {
        var adaptiveCardsUsesStringsForBools = Value ? "true" : "false";
        return $"\"{Key}\": \"{adaptiveCardsUsesStringsForBools}\"";
    }
}
