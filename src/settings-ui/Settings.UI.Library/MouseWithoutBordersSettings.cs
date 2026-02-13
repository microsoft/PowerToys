// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public class MouseWithoutBordersSettings : BasePTModuleSettings, ISettingsConfig, IHotkeyConfig
    {
        public const string ModuleName = "MouseWithoutBorders";

        private static readonly JsonSerializerOptions _serializerOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            MaxDepth = 0,
            IncludeFields = true,
        };

        [JsonPropertyName("properties")]
        public MouseWithoutBordersProperties Properties { get; set; }

        public MouseWithoutBordersSettings()
        {
            Name = ModuleName;
            Properties = new MouseWithoutBordersProperties();
            Version = "1.1";
        }

        public string GetModuleName()
        {
            return Name;
        }

        public ModuleType GetModuleType() => ModuleType.MouseWithoutBorders;

        public HotkeyAccessor[] GetAllHotkeyAccessors()
        {
            var hotkeyAccessors = new List<HotkeyAccessor>
            {
                new HotkeyAccessor(
                    () => Properties.ToggleEasyMouseShortcut,
                    value => Properties.ToggleEasyMouseShortcut = value ?? MouseWithoutBordersProperties.DefaultHotKeyToggleEasyMouse,
                    "MouseWithoutBorders_ToggleEasyMouseShortcut"),
                new HotkeyAccessor(
                    () => Properties.LockMachineShortcut,
                    value => Properties.LockMachineShortcut = value ?? MouseWithoutBordersProperties.DefaultHotKeyLockMachine,
                    "MouseWithoutBorders_LockMachinesShortcut"),
                new HotkeyAccessor(
                    () => Properties.Switch2AllPCShortcut,
                    value => Properties.Switch2AllPCShortcut = value ?? MouseWithoutBordersProperties.DefaultHotKeySwitch2AllPC,
                    "MouseWithoutBorders_Switch2AllPcShortcut"),
                new HotkeyAccessor(
                    () => Properties.ReconnectShortcut,
                    value => Properties.ReconnectShortcut = value ?? MouseWithoutBordersProperties.DefaultHotKeyReconnect,
                    "MouseWithoutBorders_ReconnectShortcut"),
            };

            return hotkeyAccessors.ToArray();
        }

        public HotkeySettings ConvertMouseWithoutBordersHotKeyToPowerToys(int value)
        {
            // VK_A <= value <= VK_Z
            if (value >= 0x41 && value <= 0x5A)
            {
                return new HotkeySettings(false, true, true, false, value);
            }
            else
            {
                // Disabled state
                return new HotkeySettings(false, false, false, false, 0);
            }
        }

        // This can be utilized in the future if the settings.json file is to be modified/deleted.
        public bool UpgradeSettingsConfiguration()
        {
#pragma warning disable CS0618 // We use obsolete members to upgrade them
            bool downgradedThenUpgraded = Version != "1.0" && (Properties.HotKeyToggleEasyMouse != null ||
                Properties.HotKeyLockMachine != null ||
                Properties.HotKeyReconnect != null ||
                Properties.HotKeySwitch2AllPC != null);

            if (Version == "1.0" || downgradedThenUpgraded)
            {
                Version = "1.1";

                if (Properties.HotKeyToggleEasyMouse != null)
                {
                    Properties.ToggleEasyMouseShortcut = ConvertMouseWithoutBordersHotKeyToPowerToys(Properties.HotKeyToggleEasyMouse.Value);
                }

                if (Properties.HotKeyLockMachine != null)
                {
                    Properties.LockMachineShortcut = ConvertMouseWithoutBordersHotKeyToPowerToys(Properties.HotKeyLockMachine.Value);
                }

                if (Properties.HotKeyReconnect != null)
                {
                    Properties.ReconnectShortcut = ConvertMouseWithoutBordersHotKeyToPowerToys(Properties.HotKeyReconnect.Value);
                }

                if (Properties.HotKeySwitch2AllPC != null)
                {
                    Properties.Switch2AllPCShortcut = ConvertMouseWithoutBordersHotKeyToPowerToys(Properties.HotKeySwitch2AllPC.Value);
                }

                Properties.HotKeyToggleEasyMouse = null;
                Properties.HotKeyLockMachine = null;
                Properties.HotKeyReconnect = null;
                Properties.HotKeySwitch2AllPC = null;

                return true;
            }

            return false;
#pragma warning restore CS0618
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
