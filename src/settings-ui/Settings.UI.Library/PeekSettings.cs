// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public class PeekSettings : BasePTModuleSettings, ISettingsConfig, IHotkeyConfig
    {
        public const string ModuleName = "Peek";
        public const string InitialModuleVersion = "0.0.1";
        public const string SpaceActivationIntroducedVersion = "0.0.2";
        public const string CurrentModuleVersion = SpaceActivationIntroducedVersion;

        private static readonly JsonSerializerOptions _serializerOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
        };

        [JsonPropertyName("properties")]
        public PeekProperties Properties { get; set; }

        public PeekSettings()
        {
            Name = ModuleName;
            Version = CurrentModuleVersion;
            Properties = new PeekProperties();
        }

        public string GetModuleName()
        {
            return Name;
        }

        public ModuleType GetModuleType() => ModuleType.Peek;

        public HotkeyAccessor[] GetAllHotkeyAccessors()
        {
            var hotkeyAccessors = new List<HotkeyAccessor>
            {
                new HotkeyAccessor(
                    () => Properties.ActivationShortcut,
                    value => Properties.ActivationShortcut = value ?? Properties.DefaultActivationShortcut,
                    "Activation_Shortcut"),
            };

            return hotkeyAccessors.ToArray();
        }

        public bool UpgradeSettingsConfiguration()
        {
            if (string.IsNullOrEmpty(Version) ||
                Version.Equals(InitialModuleVersion, StringComparison.OrdinalIgnoreCase))
            {
                Version = CurrentModuleVersion;
                Properties.EnableSpaceToActivate.Value = false;
                return true;
            }

            return false;
        }

        public virtual void Save(SettingsUtils settingsUtils)
        {
            // Save settings to file
            var options = _serializerOptions;

            ArgumentNullException.ThrowIfNull(settingsUtils);

            settingsUtils.SaveSettings(JsonSerializer.Serialize(this, options), ModuleName);
        }
    }
}
