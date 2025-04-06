// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Microsoft.CommandPalette.Extensions.Toolkit;

public sealed class ChoiceSetSetting : Setting<string>
{
    public partial class Choice
    {
        [JsonPropertyName("value")]
        public string Value { get; set; }

        [JsonPropertyName("title")]
        public string Title { get; set; }

        public Choice(string title, string value)
        {
            Value = value;
            Title = title;
        }
    }

    public List<Choice> Choices { get; set; }

    private ChoiceSetSetting()
        : base()
    {
        Choices = [];
    }

    public ChoiceSetSetting(string key, List<Choice> choices)
        : base(key, choices.ElementAt(0).Value)
    {
        Choices = choices;
    }

    public ChoiceSetSetting(string key, string label, string description, List<Choice> choices)
        : base(key, label, description, choices.ElementAt(0).Value)
    {
        Choices = choices;
    }

    public override Dictionary<string, object> ToDictionary()
    {
        return new Dictionary<string, object>
        {
            { "type", "Input.ChoiceSet" },
            { "title", Label },
            { "id", Key },
            { "label", Description },
            { "choices", Choices },
            { "value", Value ?? string.Empty },
            { "isRequired", IsRequired },
            { "errorMessage", ErrorMessage },
        };
    }

    public static ChoiceSetSetting LoadFromJson(JsonObject jsonObject) => new() { Value = jsonObject["value"]?.GetValue<string>() ?? string.Empty };

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
