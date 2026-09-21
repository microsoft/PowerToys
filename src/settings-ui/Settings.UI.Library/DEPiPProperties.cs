// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public class DEPiPProperties
    {
        public const int DefaultInactiveTransparency = 14;
        public const bool DefaultLockAspectRatio = false;
        public const bool DefaultAlwaysOnTop = false;

        public DEPiPProperties()
        {
            InactiveTransparency = new IntProperty(DefaultInactiveTransparency);
            LockAspectRatio = new BoolProperty(DefaultLockAspectRatio);
            AlwaysOnTop = new BoolProperty(DefaultAlwaysOnTop);
        }

        [JsonPropertyName("inactiveTransparency")]
        public IntProperty InactiveTransparency { get; set; }

        [JsonPropertyName("lockAspectRatio")]
        public BoolProperty LockAspectRatio { get; set; }

        [JsonPropertyName("alwaysOnTop")]
        public BoolProperty AlwaysOnTop { get; set; }
    }
}
