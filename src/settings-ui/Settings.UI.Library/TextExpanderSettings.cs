// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;

using Microsoft.PowerToys.Settings.UI.Library.Interfaces;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public class TextExpanderSettings : BasePTModuleSettings, ISettingsConfig
    {
        public const string ModuleName = "TextExpander";
        public const string ModuleVersion = "0.0.1";

        // Replacements at least this long are pasted rather than typed. Kept low on purpose:
        // per-character injection is not reliable under load (measured at 5ms/char it corrupted
        // 2 of 9 samples, and 15ms still smeared text), while a paste is a single Ctrl+V
        // regardless of length. Must match InjectionPolicy.DefaultClipboardThresholdChars.
        public const int DefaultClipboardThresholdChars = 5;

        [JsonPropertyName("properties")]
        public TextExpanderProperties Properties { get; set; }

        public TextExpanderSettings()
        {
            Name = ModuleName;
            Version = ModuleVersion;
            Properties = new TextExpanderProperties();
        }

        public string GetModuleName()
        {
            return Name;
        }

        public bool UpgradeSettingsConfiguration()
        {
            return false;
        }
    }
}
