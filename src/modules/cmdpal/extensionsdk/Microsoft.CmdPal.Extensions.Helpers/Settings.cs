// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using System.Text.Json.Nodes;
using Windows.Foundation;

namespace Microsoft.CmdPal.Extensions.Helpers;

public sealed class Settings
{
    private readonly Dictionary<string, object> _settings = new();
    private static readonly JsonSerializerOptions _jsonSerializerOptions = new() { WriteIndented = true };

    public event TypedEventHandler<object, Settings>? SettingsChanged;

    public void Add<T>(Setting<T> s)
    {
        _settings.Add(s.Key, s);
    }

    public T? GetSetting<T>(string key)
    {
        return _settings[key] is Setting<T> s ? s.Value : default;
    }

    public bool TryGetSetting<T>(string key, out T? val)
    {
        object? o;
        if (_settings.TryGetValue(key, out o))
        {
            if (o is Setting<T> s)
            {
                val = s.Value;
                return true;
            }
        }

        val = default;
        return false;
    }

    internal string ToFormJson()
    {
        var settings = _settings
            .Values
            .Where(s => s is ISettingsForm)
            .Select(s => s as ISettingsForm)
            .Where(s => s != null)
            .Select(s => s!);

        var bodies = string.Join(",", settings
            .Select(s => JsonSerializer.Serialize(s.ToDictionary(), _jsonSerializerOptions)));
        var datas = string.Join(",", settings
            .Select(s => s.ToDataIdentifier()));

        var json = $$"""
{
  "$schema": "http://adaptivecards.io/schemas/adaptive-card.json",
  "type": "AdaptiveCard",
  "version": "1.5",
  "body": [
      {{bodies}} 
  ],
  "actions": [
    {
      "type": "Action.Submit",
      "title": "Save",
      "data": {
        {{datas}}
      }
    }
  ]
}
""";
        return json;
    }

    public IForm[] ToForms()
    {
        return [new SettingsForm(this)];
    }

    public void Update(string data)
    {
        var formInput = JsonNode.Parse(data)?.AsObject();
        if (formInput == null)
        {
            return;
        }

        foreach (var key in _settings.Keys)
        {
            var value = _settings[key];
            if (value is ISettingsForm f)
            {
                f.Update(formInput);
            }
        }
    }

    internal void RaiseSettingsChanged()
    {
        var handlers = SettingsChanged;
        handlers?.Invoke(this, this);
    }
}
