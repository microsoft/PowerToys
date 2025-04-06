// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using System.Text.Json.Nodes;

namespace Microsoft.CommandPalette.Extensions.Toolkit;

public partial class TextSetting : Setting<string>
{
    public bool Multiline { get; set; }

    public string Placeholder { get; set; } = string.Empty;

    private TextSetting()
        : base()
    {
        Value = string.Empty;
    }

    public TextSetting(string key, string defaultValue)
        : base(key, defaultValue)
    {
    }

    public TextSetting(string key, string label, string description, string defaultValue)
        : base(key, label, description, defaultValue)
    {
    }

    public override Dictionary<string, object> ToDictionary()
    {
        return new Dictionary<string, object>
        {
            { "type", "Input.Text" },
            { "title", Label },
            { "id", Key },
            { "label", Description },
            { "value", Value ?? string.Empty },
            { "isRequired", IsRequired },
            { "errorMessage", ErrorMessage },
            { "isMultiline", Multiline },
            { "placeholder", Placeholder },
        };
    }

    public static TextSetting LoadFromJson(JsonObject jsonObject) => new() { Value = jsonObject["value"]?.GetValue<string>() ?? string.Empty };

    public override void Update(JsonObject payload)
    {
        // If the key doesn't exist in the payload, don't do anything
        if (payload[Key] != null)
        {
            Value = payload[Key]?.GetValue<string>();
        }
    }

    public override string ToState() => $"\"{Key}\": {JsonSerializer.Serialize(Value, JsonSerializationContext.Default.String)}";
}
