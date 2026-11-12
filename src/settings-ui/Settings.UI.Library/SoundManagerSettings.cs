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
    public class SoundManagerSettings : BasePTModuleSettings, ISettingsConfig, IHotkeyConfig
    {
        public const string ModuleName = "Sound Manager";

        private static readonly JsonSerializerOptions _serializerOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
        };

        [JsonPropertyName("properties")]
        public SoundManagerProperties Properties { get; set; }

        public SoundManagerSettings()
        {
            Properties = new SoundManagerProperties();
            Version = "1.0";
            Name = ModuleName;
        }

        public virtual void Save(SettingsUtils settingsUtils)
        {
            var options = _serializerOptions;

            ArgumentNullException.ThrowIfNull(settingsUtils);

            settingsUtils.SaveSettings(JsonSerializer.Serialize(this, options), ModuleName);
        }

        public string GetModuleName() => Name;

        public ModuleType GetModuleType() => ModuleType.SoundManager;

        public HotkeyAccessor[] GetAllHotkeyAccessors()
        {
            var hotkeyAccessors = new List<HotkeyAccessor>
            {
                new HotkeyAccessor(
                    () => Properties.ActivationShortcut,
                    value => Properties.ActivationShortcut = value ?? Properties.DefaultActivationShortcut,
                    "SoundManager_ActivationShortcut"),
            };

            // The runner gives every hotkey of the module a number, which is its place in this list. The programs come after the one of the window.
            if (Properties.MuteShortcuts != null)
            {
                foreach (var muteShortcut in Properties.MuteShortcuts.Value)
                {
                    hotkeyAccessors.Add(new HotkeyAccessor(
                        () => muteShortcut.Shortcut,
                        value => muteShortcut.Shortcut = value ?? new HotkeySettings(),
                        muteShortcut.Name));
                }
            }

            return hotkeyAccessors.ToArray();
        }

        public bool UpgradeSettingsConfiguration() => false;
    }
}
