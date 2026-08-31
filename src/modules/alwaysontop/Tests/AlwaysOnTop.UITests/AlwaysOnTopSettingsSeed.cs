// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Nodes;
using Microsoft.PowerToys.UITest.Next;

namespace Microsoft.AlwaysOnTop.UITests;

internal static class AlwaysOnTopSettingsSeed
{
    internal const string ModuleName = "AlwaysOnTop";

    private const string DefaultSettings = """
        {
          "name": "AlwaysOnTop",
          "properties": {},
          "version": "0.0.1"
        }
        """;

    internal static void ApplyBaseline()
    {
        SettingsConfigHelper.UpdateModuleSettings(
            ModuleName,
            DefaultSettings,
            root =>
            {
                var properties = root["properties"]?.AsObject()
                    ?? throw new InvalidOperationException("Always On Top settings have no properties object.");
                SetValue(properties, "frame-enabled", true);
                SetValue(properties, "frame-thickness", 4);
                SetValue(properties, "frame-color", "#0099CC");
                SetValue(properties, "frame-opacity", 100);
                SetValue(properties, "frame-accent-color", false);
                SetValue(properties, "sound-enabled", false);
                SetValue(properties, "show-in-system-menu", false);
                SetValue(properties, "do-not-activate-on-game-mode", false);
                SetValue(properties, "excluded-apps", string.Empty);
                SetValue(properties, "round-corners-enabled", false);
                properties["hotkey"] = new JsonObject
                {
                    ["value"] = new JsonObject
                    {
                        ["win"] = true,
                        ["ctrl"] = true,
                        ["alt"] = false,
                        ["shift"] = false,
                        ["code"] = (int)'T',
                        ["key"] = string.Empty,
                    },
                };
            });
    }

    internal static void Apply(params (string Name, JsonNode? Value)[] settings)
    {
        SettingsConfigHelper.UpdateModuleSettings(
            ModuleName,
            DefaultSettings,
            root =>
            {
                if (root["properties"] is not JsonObject properties)
                {
                    properties = new JsonObject();
                    root["properties"] = properties;
                }

                foreach (var (name, value) in settings)
                {
                    SetValue(properties, name, value);
                }
            });
    }

    private static void SetValue(JsonObject properties, string name, JsonNode? value)
    {
        properties[name] = new JsonObject
        {
            ["value"] = value?.DeepClone(),
        };
    }
}
