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
        return new Dictionary<string, object>
        {
            { "type", "Input.Toggle" },
            { "title", Label },
            { "id", Key },
            { "label", Description },
            { "value", JsonSerializer.Serialize(Value, JsonSerializationContext.Default.Boolean) },
            { "isRequired", IsRequired },
            { "errorMessage", ErrorMessage },
        };
    }

    public static ToggleSetting LoadFromJson(JsonObject jsonObject) => new() { Value = jsonObject["value"]?.GetValue<bool>() ?? false };

    public override void Update(JsonObject payload)
    {
        // If the key doesn't exist in the payload, don't do anything
        if (payload[Key] != null)
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
