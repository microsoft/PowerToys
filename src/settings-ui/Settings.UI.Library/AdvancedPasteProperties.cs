// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
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
            EnableClipboardPreview = true;
            PasteAIConfiguration = new();
        }

        [JsonConverter(typeof(BoolPropertyJsonConverter))]
        public bool IsAIEnabled { get; set; }

        private bool? _legacyAdvancedAIEnabled;

        [JsonPropertyName("IsAdvancedAIEnabled")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public BoolProperty LegacyAdvancedAIEnabledProperty
        {
            get => null;
            set
            {
                if (value is not null)
                {
                    LegacyAdvancedAIEnabled = value.Value;
                }
            }
        }

        [JsonIgnore]
        public bool? LegacyAdvancedAIEnabled
        {
            get => _legacyAdvancedAIEnabled;
            private set => _legacyAdvancedAIEnabled = value;
        }

        public bool TryConsumeLegacyAdvancedAIEnabled(out bool value)
        {
            if (_legacyAdvancedAIEnabled is bool flag)
            {
                value = flag;
                _legacyAdvancedAIEnabled = null;
                return true;
            }

            value = default;
            return false;
        }

        [JsonConverter(typeof(BoolPropertyJsonConverter))]
        public bool ShowCustomPreview { get; set; }

        [JsonConverter(typeof(BoolPropertyJsonConverter))]
        public bool CloseAfterLosingFocus { get; set; }

        [JsonConverter(typeof(BoolPropertyJsonConverter))]
        public bool EnableClipboardPreview { get; set; }

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

        public override string ToString()
            => JsonSerializer.Serialize(this);
    }
}
