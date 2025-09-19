// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;

using Settings.UI.Library.Attributes;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public class PowerOcrProperties
    {
        [CmdConfigureIgnore]
        public HotkeySettings DefaultActivationShortcut => new HotkeySettings(true, false, false, true, 0x54); // Win+Shift+T

        public PowerOcrProperties()
        {
            ActivationShortcut = DefaultActivationShortcut;
            PreferredLanguage = string.Empty;
            UseLocalAIIfAvailable = true;
        }

        public HotkeySettings ActivationShortcut { get; set; }

        public string PreferredLanguage { get; set; }

        // New: whether to attempt local AI-based OCR when available (fallback handled internally)
        public bool UseLocalAIIfAvailable { get; set; }

        public override string ToString()
            => JsonSerializer.Serialize(this);
    }
}
