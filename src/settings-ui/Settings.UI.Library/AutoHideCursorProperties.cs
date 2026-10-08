// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public class AutoHideCursorProperties
    {
        public const int DefaultIdleDelayMs = 5000;
        public const int MinimumIdleDelayMs = 1000;
        public const int MaximumIdleDelayMs = 60000;

        [JsonPropertyName("hide_on_typing")]
        public BoolProperty HideOnTyping { get; set; }

        [JsonPropertyName("hide_on_idle")]
        public BoolProperty HideOnIdle { get; set; }

        [JsonPropertyName("idle_delay_ms")]
        public IntProperty IdleDelayMs { get; set; }

        public AutoHideCursorProperties()
        {
            HideOnTyping = new BoolProperty(true);
            HideOnIdle = new BoolProperty(false);
            IdleDelayMs = new IntProperty(DefaultIdleDelayMs);
        }
    }
}
