// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using Settings.UI.Library.Attributes;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public class ScreenTranslatorProperties
    {
        [CmdConfigureIgnore]
        public HotkeySettings DefaultActivationShortcut => new HotkeySettings(true, false, true, false, 0x54); // Win+Ctrl+T

        public ScreenTranslatorProperties()
        {
            ActivationShortcut = DefaultActivationShortcut;
            SelectedProvider = "Passthrough";
            EnableCloudConsent = false;
            SourceLanguage = "auto";
            TargetLanguage = "en-US";
            AzureEndpoint = "https://api.cognitive.microsofttranslator.com";
            AzureRegion = string.Empty;
            LibreTranslateEndpoint = "http://localhost:5000";
        }

        [JsonPropertyName("ActivationShortcut")]
        public HotkeySettings ActivationShortcut { get; set; }

        [JsonPropertyName("SelectedProvider")]
        public string SelectedProvider { get; set; }

        [JsonPropertyName("EnableCloudConsent")]
        public bool EnableCloudConsent { get; set; }

        [JsonPropertyName("SourceLanguage")]
        public string SourceLanguage { get; set; }

        [JsonPropertyName("TargetLanguage")]
        public string TargetLanguage { get; set; }

        [JsonPropertyName("AzureEndpoint")]
        public string AzureEndpoint { get; set; }

        [JsonPropertyName("AzureRegion")]
        public string AzureRegion { get; set; }

        [JsonPropertyName("LibreTranslateEndpoint")]
        public string LibreTranslateEndpoint { get; set; }

        public override string ToString()
            => JsonSerializer.Serialize(this, SettingsSerializationContext.Default.ScreenTranslatorProperties);
    }
}
