// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Text.Json.Serialization;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public class AutoHideCursorSettings : BasePTModuleSettings, ISettingsConfig
    {
        public const string ModuleName = "AutoHideCursor";

        [JsonPropertyName("properties")]
        public AutoHideCursorProperties Properties { get; set; }

        public AutoHideCursorSettings()
        {
            Name = ModuleName;
            Properties = new AutoHideCursorProperties();
            Version = "1.0";
        }

        public string GetModuleName() => Name;

        public ModuleType GetModuleType() => ModuleType.AutoHideCursor;

        public bool UpgradeSettingsConfiguration()
        {
            bool settingsUpgraded = false;
            if (Properties == null)
            {
                Properties = new AutoHideCursorProperties();
                return true;
            }

            if (Properties.HideOnTyping == null)
            {
                Properties.HideOnTyping = new BoolProperty(true);
                settingsUpgraded = true;
            }

            if (Properties.HideOnIdle == null)
            {
                Properties.HideOnIdle = new BoolProperty(false);
                settingsUpgraded = true;
            }

            int normalizedIdleDelay = Math.Clamp(
                Properties.IdleDelayMs?.Value ?? AutoHideCursorProperties.DefaultIdleDelayMs,
                AutoHideCursorProperties.MinimumIdleDelayMs,
                AutoHideCursorProperties.MaximumIdleDelayMs);
            if (Properties.IdleDelayMs == null || Properties.IdleDelayMs.Value != normalizedIdleDelay)
            {
                Properties.IdleDelayMs = new IntProperty(normalizedIdleDelay);
                settingsUpgraded = true;
            }

            return settingsUpgraded;
        }
    }
}
