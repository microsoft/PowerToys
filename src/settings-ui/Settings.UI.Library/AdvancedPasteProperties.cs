// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

using Settings.UI.Library.Attributes;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public class AdvancedPasteProperties
    {
        public static readonly HotkeySettings DefaultAdvancedPasteUIShortcut = new HotkeySettings(true, false, false, true, 0x56); // Win+Shift+V

        public static readonly HotkeySettings DefaultPasteAsPlainTextShortcut = new HotkeySettings(true, true, true, false, 0x56); // Ctrl+Win+Alt+V

        public AdvancedPasteProperties()
        {
            AdvancedPasteUIShortcut = DefaultAdvancedPasteUIShortcut;
            PasteAsPlainTextShortcut = DefaultPasteAsPlainTextShortcut;
            PasteAsMarkdownShortcut = new();
            PasteAsJsonShortcut = new();
            CustomActions = new();
            AdditionalActions = new();
            IsAIEnabled = false;
            ShowCustomPreview = true;
            CloseAfterLosingFocus = false;
            PasteAIConfiguration = new();
            CustomModelStoragePath = GetDefaultCustomModelStoragePath();
        }

        [JsonConverter(typeof(BoolPropertyJsonConverter))]
        public bool IsAIEnabled { get; set; }

        [JsonExtensionData]
        public Dictionary<string, JsonElement> ExtensionData
        {
            get => _extensionData;
            set
            {
                _extensionData = value;

                if (_extensionData != null && _extensionData.TryGetValue("IsOpenAIEnabled", out var legacyElement) && legacyElement.ValueKind == JsonValueKind.Object && legacyElement.TryGetProperty("value", out var valueElement))
                {
                    IsAIEnabled = valueElement.ValueKind switch
                    {
                        JsonValueKind.True => true,
                        JsonValueKind.False => false,
                        _ => IsAIEnabled,
                    };

                    _extensionData.Remove("IsOpenAIEnabled");
                }
            }
        }

        private Dictionary<string, JsonElement> _extensionData;

        [JsonConverter(typeof(BoolPropertyJsonConverter))]
        public bool ShowCustomPreview { get; set; }

        [JsonConverter(typeof(BoolPropertyJsonConverter))]
        public bool CloseAfterLosingFocus { get; set; }

        [JsonPropertyName("advanced-paste-ui-hotkey")]
        public HotkeySettings AdvancedPasteUIShortcut { get; set; }

        [JsonPropertyName("paste-as-plain-hotkey")]
        public HotkeySettings PasteAsPlainTextShortcut { get; set; }

        [JsonPropertyName("paste-as-markdown-hotkey")]
        public HotkeySettings PasteAsMarkdownShortcut { get; set; }

        [JsonPropertyName("paste-as-json-hotkey")]
        public HotkeySettings PasteAsJsonShortcut { get; set; }

        [JsonPropertyName("custom-actions")]
        [CmdConfigureIgnoreAttribute]
        public AdvancedPasteCustomActions CustomActions { get; init; }

        [JsonPropertyName("additional-actions")]
        [CmdConfigureIgnoreAttribute]
        public AdvancedPasteAdditionalActions AdditionalActions { get; init; }

        [JsonPropertyName("paste-ai-configuration")]
        [CmdConfigureIgnoreAttribute]
        public PasteAIConfiguration PasteAIConfiguration { get; set; }

        [JsonPropertyName("custom-model-storage-path")]
        [CmdConfigureIgnoreAttribute]
        public string CustomModelStoragePath { get; set; }

        public override string ToString()
            => JsonSerializer.Serialize(this);

        public static string GetDefaultCustomModelStoragePath()
        {
            try
            {
                var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                if (string.IsNullOrWhiteSpace(userProfile))
                {
                    userProfile = Environment.GetEnvironmentVariable("USERPROFILE");
                }

                if (string.IsNullOrWhiteSpace(userProfile))
                {
                    var temp = Path.GetTempPath();
                    return Path.Combine(temp, "PowerToys", "Models");
                }

                return Path.Combine(userProfile, ".cache", "PowerToys");
            }
            catch (Exception)
            {
                var temp = Path.GetTempPath();
                return Path.Combine(temp, "PowerToys", "Models");
            }
        }
    }
}
