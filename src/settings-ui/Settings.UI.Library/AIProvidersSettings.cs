// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    /// <summary>
    /// Global AI provider settings shared by all PowerToys modules.
    /// </summary>
    public class AIProvidersSettings : BasePTModuleSettings, ISettingsConfig
    {
        public const string ModuleName = "AIProviders";

        private static readonly JsonSerializerOptions SerializerOptions = new()
        {
            WriteIndented = true,
        };

        [JsonPropertyName("properties")]
        public AIProvidersProperties Properties { get; set; }

        public AIProvidersSettings()
        {
            Properties = new AIProvidersProperties();
            Version = "1.0";
            Name = ModuleName;
        }

        public virtual void Save(SettingsUtils settingsUtils)
        {
            ArgumentNullException.ThrowIfNull(settingsUtils);
            settingsUtils.SaveSettings(JsonSerializer.Serialize(this, SerializerOptions), ModuleName);
        }

        public string GetModuleName() => Name;

        public bool UpgradeSettingsConfiguration() => false;
    }
}
