// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using System.Text.Json.Nodes;

namespace Microsoft.CommandPalette.Extensions.Toolkit;

public partial class TextCardSetting : Setting<string>
{
    public bool Multiline { get; set; }

    public string Placeholder { get; set; } = string.Empty;

    private TextCardSetting()
        : base()
    {
        Value = string.Empty;
    }

    public TextCardSetting(string key, string defaultValue)
        : base(key, defaultValue)
    {
    }

    public TextCardSetting(string key, string label, string description, string defaultValue)
        : base(key, label, description, defaultValue)
    {
    }

    public override Dictionary<string, object> ToDictionary()
    {
        var type = Multiline ? "SettingsCard.Input.TextArea" : "SettingsCard.Input.Text";
        return new Dictionary<string, object>
        {
            { "type", type },
            { "id", Key },
            { "header", Label },
            { "description", Description },
            { "value", Value ?? string.Empty },
            { "isRequired", IsRequired },
            { "errorMessage", ErrorMessage },
            { "isMultiline", Multiline },
            { "placeholder", Placeholder },
            { "label", string.Empty },
        };
    }

    public static TextCardSetting LoadFromJson(JsonObject jsonObject) => new() { Value = jsonObject["value"]?.GetValue<string>() ?? string.Empty };

    public override void Update(JsonObject payload)
    {
        // If the key doesn't exist in the payload, don't do anything
        if (payload[Key] is not null)
        {
            Value = payload[Key]?.GetValue<string>();
        }
    }

    public override string ToState() => $"\"{Key}\": {JsonSerializer.Serialize(Value, JsonSerializationContext.Default.String)}";
}
