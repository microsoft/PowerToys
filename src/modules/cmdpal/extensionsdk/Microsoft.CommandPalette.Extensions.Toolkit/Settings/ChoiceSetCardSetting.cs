// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Microsoft.CommandPalette.Extensions.Toolkit;

public sealed class ChoiceSetCardSetting : Setting<string>
{
    public partial class Entry
    {
        [JsonPropertyName("value")]
        public string Value { get; set; }

        [JsonPropertyName("title")]
        public string Title { get; set; }

        public Entry(string title, string value)
        {
            Value = value;
            Title = title;
        }
    }

    public List<Entry> Choices { get; set; }

    private ChoiceSetCardSetting()
    {
        Choices = [];
    }

    public ChoiceSetCardSetting(string key, List<Entry> choices)
        : base(key, choices.ElementAt(0).Value)
    {
        Choices = choices;
    }

    public ChoiceSetCardSetting(string key, string label, string description, List<Entry> choices)
        : base(key, label, description, choices.ElementAt(0).Value)
    {
        Choices = choices;
    }

    public ChoiceSetCardSetting(string key, string label, string description, List<Entry> choices, string defaultValue)
        : base(key, label, description, defaultValue)
    {
        Choices = choices;
    }

    public override Dictionary<string, object> ToDictionary()
    {
        return new Dictionary<string, object>
        {
            { "type", "SettingsCard.Input.ComboBox" },
            { "id", Key },
            { "choices", Choices },
            { "label", string.Empty },
            { "header", Label },
            { "description", Description },
            { "value", Value ?? string.Empty },
            { "isRequired", IsRequired },
            { "errorMessage", ErrorMessage },
        };
    }

    public static ChoiceSetCardSetting LoadFromJson(JsonObject jsonObject) => new() { Value = jsonObject["value"]?.GetValue<string>() ?? string.Empty };

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
