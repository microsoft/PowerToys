// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

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
        var bodyJson = JsonSerializer.Serialize(ToDictionary(), JsonSerializationContext.Default.Dictionary);
        var dataJson = $"\"{Key}\": \"{Key}\"";

        var json = $$"""
{
  "$schema": "http://adaptivecards.io/schemas/adaptive-card.json",
  "type": "AdaptiveCard",
  "version": "1.5",
  "body": [
      {{bodyJson}}
  ],
  "actions": [
    {
      "type": "Action.Submit",
      "title": "Save",
      "data": {
        {{dataJson}}
      }
    }
  ]
}
""";
        return json;
    }

    public abstract void Update(JsonObject payload);

    public abstract string ToState();
}
