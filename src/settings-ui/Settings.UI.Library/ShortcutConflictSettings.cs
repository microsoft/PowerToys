// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public class ShortcutConflictSettings : ISettingsConfig
    {
        public const string ModuleName = "ShortcutConflicts";

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("version")]
        public string Version { get; set; }

        [JsonPropertyName("properties")]
        public ShortcutConflictProperties Properties { get; set; }

        public ShortcutConflictSettings()
        {
            Name = ModuleName;
            Version = "1.0";
            Properties = new ShortcutConflictProperties();
        }

        public string ToJsonString()
        {
            return JsonSerializer.Serialize(this);
        }

        public string GetModuleName()
        {
            return Name;
        }

        public bool UpgradeSettingsConfiguration()
        {
            return false; // No upgrades needed for now
        }
    }
}
