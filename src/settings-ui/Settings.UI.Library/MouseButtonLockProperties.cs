// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public class MouseButtonLockProperties
    {
        [JsonPropertyName("lmb_lock_enabled")]
        public BoolProperty LmbLockEnabled { get; set; }

        [JsonPropertyName("rmb_lock_enabled")]
        public BoolProperty RmbLockEnabled { get; set; }

        [JsonPropertyName("mmb_lock_enabled")]
        public BoolProperty MmbLockEnabled { get; set; }

        [JsonPropertyName("hold_duration_ms")]
        public IntProperty HoldDurationMs { get; set; }

        [JsonPropertyName("move_cancel_enabled")]
        public BoolProperty MoveCancelEnabled { get; set; }

        [JsonPropertyName("move_cancel_pixels")]
        public IntProperty MoveCancelPixels { get; set; }

        public MouseButtonLockProperties()
        {
            LmbLockEnabled = new BoolProperty(false);
            RmbLockEnabled = new BoolProperty(true);
            MmbLockEnabled = new BoolProperty(false);
            HoldDurationMs = new IntProperty(300);
            MoveCancelEnabled = new BoolProperty(true);
            MoveCancelPixels = new IntProperty(5);
        }
    }
}
