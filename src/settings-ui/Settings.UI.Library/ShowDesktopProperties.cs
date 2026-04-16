// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public class ShowDesktopProperties
    {
        public const int DefaultPeekMode = 0;
        public const bool DefaultRequireDoubleClick = false;
        public const bool DefaultEnableTaskbarPeek = false;
        public const bool DefaultEnableGamingDetection = true;
        public const int DefaultFlyAwayAnimationDurationMs = 300;

        public ShowDesktopProperties()
        {
            PeekMode = new IntProperty(DefaultPeekMode);
            RequireDoubleClick = new BoolProperty(DefaultRequireDoubleClick);
            EnableTaskbarPeek = new BoolProperty(DefaultEnableTaskbarPeek);
            EnableGamingDetection = new BoolProperty(DefaultEnableGamingDetection);
            FlyAwayAnimationDurationMs = new IntProperty(DefaultFlyAwayAnimationDurationMs);
        }

        [JsonPropertyName("peek-mode")]
        public IntProperty PeekMode { get; set; }

        [JsonPropertyName("require-double-click")]
        public BoolProperty RequireDoubleClick { get; set; }

        [JsonPropertyName("enable-taskbar-peek")]
        public BoolProperty EnableTaskbarPeek { get; set; }

        [JsonPropertyName("enable-gaming-detection")]
        public BoolProperty EnableGamingDetection { get; set; }

        [JsonPropertyName("fly-away-animation-duration-ms")]
        public IntProperty FlyAwayAnimationDurationMs { get; set; }

        public string ToJsonString()
        {
            return JsonSerializer.Serialize(this);
        }
    }
}
