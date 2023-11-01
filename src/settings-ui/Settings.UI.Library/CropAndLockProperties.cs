// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public class CropAndLockProperties
    {
        public static readonly HotkeySettings DefaultReparentHotkeyValue = new HotkeySettings(true, true, false, true, 0x52); // Ctrl+Win+Shift+R
        public static readonly HotkeySettings DefaultThumbnailHotkeyValue = new HotkeySettings(true, true, false, true, 0x54); // Ctrl+Win+Shift+T

        public CropAndLockProperties()
        {
            ReparentHotkey = new KeyboardKeysProperty(DefaultReparentHotkeyValue);
            ThumbnailHotkey = new KeyboardKeysProperty(DefaultThumbnailHotkeyValue);
        }

        [JsonPropertyName("reparent-hotkey")]
        public KeyboardKeysProperty ReparentHotkey { get; set; }

        [JsonPropertyName("thumbnail-hotkey")]
        public KeyboardKeysProperty ThumbnailHotkey { get; set; }
    }
}
