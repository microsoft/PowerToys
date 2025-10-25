// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using System.Text.Json.Nodes;
using Windows.Foundation;

namespace Microsoft.CommandPalette.Extensions.Toolkit;

public sealed partial class Settings : ICommandSettings
{
    private readonly Dictionary<string, object> _settings = [];
    private static readonly JsonSerializerOptions _jsonSerializerOptions = new() { WriteIndented = true };

    public event TypedEventHandler<object, Settings>? SettingsChanged;

    public void Add<T>(Setting<T> s) => _settings.Add(s.Key, s);

    public T? GetSetting<T>(string key) => _settings[key] is Setting<T> s ? s.Value : default;

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
            .Where(s => s is not null)
            .Select(s => s!)
            .ToList();

        var bodyElements = new JsonArray();

        foreach (var setting in settings)
        {
            var formJson = setting.ToForm();
            if (string.IsNullOrWhiteSpace(formJson))
            {
                continue;
            }

            var formNode = JsonNode.Parse(formJson) as JsonObject;
            var body = formNode?["body"] as JsonArray;
            if (body is null)
            {
                continue;
            }

            foreach (var element in body)
            {
                if (element is not null)
                {
                    bodyElements.Add(element.DeepClone());
                }
            }
        }

        var dataIdentifiers = string.Join(",", settings.Select(s => s.ToDataIdentifier()));
        var dataNode = new JsonObject();
        if (!string.IsNullOrWhiteSpace(dataIdentifiers))
        {
            dataNode = JsonNode.Parse($"{{{dataIdentifiers}}}")?.AsObject() ?? new JsonObject();
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
                    ["data"] = dataNode
                }
            }
        };

        return card.ToJsonString(_jsonSerializerOptions);
    }

    public string ToJson()
    {
        var settings = _settings
            .Values
            .Where(s => s is ISettingsForm)
            .Select(s => s as ISettingsForm)
            .Where(s => s is not null)
            .Select(s => s!);
        var content = string.Join(",\n", settings.Select(s => s.ToState()));
        return $"{{\n{content}\n}}";
    }

    public void Update(string data)
    {
        var formInput = JsonNode.Parse(data)?.AsObject();
        if (formInput is null)
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

    private sealed partial class SettingsContentPage : ContentPage
    {
        private readonly Settings _settings;

        public override IContent[] GetContent() => _settings.ToContent();

        public SettingsContentPage(Settings settings)
        {
            _settings = settings;
            Name = Properties.Resources.Settings;
            Icon = new IconInfo("\uE713"); // Settings icon

            // When our settings change, make sure to let CmdPal know to
            // retrieve the new forms
            _settings.SettingsChanged += (s, e) => RaiseItemsChanged();
        }
    }

    public IContentPage SettingsPage => new SettingsContentPage(this);

    public IContent[] ToContent() => [new SettingsForm(this)];
}
