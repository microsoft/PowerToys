// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using PowerDisplay.Common.Models;
using static PowerDisplay.Common.Drivers.NativeConstants;

namespace PowerDisplay.Common.Utils
{
    /// <summary>
    /// Maps each <see cref="VcpFeature"/> to its ordered list of candidate VCP codes
    /// (highest priority first) and a stable persistence/diagnostic key. Candidate
    /// values come from <see cref="PowerDisplay.Common.Drivers.NativeConstants"/>.
    /// Only brightness has multiple candidates today; the rest are single-candidate.
    /// </summary>
    public static class VcpFeatureRegistry
    {
        private static readonly VcpFeature[] AllFeaturesArray =
        {
            VcpFeature.Brightness,
            VcpFeature.Contrast,
            VcpFeature.Volume,
            VcpFeature.ColorTemperature,
            VcpFeature.InputSource,
            VcpFeature.PowerState,
        };

        private static readonly Dictionary<VcpFeature, byte[]> CandidatesByFeature = new()
        {
            [VcpFeature.Brightness] = new[] { VcpCodeBrightness, VcpCodeBacklightControl, VcpCodeBacklightLevelWhite },
            [VcpFeature.Contrast] = new[] { VcpCodeContrast },
            [VcpFeature.Volume] = new[] { VcpCodeVolume },
            [VcpFeature.ColorTemperature] = new[] { VcpCodeSelectColorPreset },
            [VcpFeature.InputSource] = new[] { VcpCodeInputSource },
            [VcpFeature.PowerState] = new[] { VcpCodePowerMode },
        };

        private static readonly Dictionary<VcpFeature, string> KeysByFeature = new()
        {
            [VcpFeature.Brightness] = "brightness",
            [VcpFeature.Contrast] = "contrast",
            [VcpFeature.Volume] = "volume",
            [VcpFeature.ColorTemperature] = "colorTemperature",
            [VcpFeature.InputSource] = "inputSource",
            [VcpFeature.PowerState] = "powerState",
        };

        /// <summary>
        /// All supported <see cref="VcpFeature"/> values in display order.
        /// </summary>
        public static IReadOnlyList<VcpFeature> AllFeatures => AllFeaturesArray;

        /// <summary>
        /// Returns the ordered list of candidate VCP codes for <paramref name="feature"/>,
        /// highest priority first.
        /// </summary>
        /// <param name="feature">The feature whose candidates are requested.</param>
        /// <returns>Ordered candidate byte values (highest priority first).</returns>
        public static IReadOnlyList<byte> Candidates(VcpFeature feature) => CandidatesByFeature[feature];

        /// <summary>
        /// Returns the highest-priority VCP code for <paramref name="feature"/>.
        /// </summary>
        /// <param name="feature">The feature whose primary code is requested.</param>
        /// <returns>The first (highest-priority) candidate VCP code.</returns>
        public static byte Primary(VcpFeature feature) => CandidatesByFeature[feature][0];

        /// <summary>
        /// Returns the stable persistence/diagnostic key string for <paramref name="feature"/>
        /// (e.g., <c>"brightness"</c>).
        /// </summary>
        /// <param name="feature">The feature whose key is requested.</param>
        /// <returns>Lowercase camelCase key string.</returns>
        public static string Key(VcpFeature feature) => KeysByFeature[feature];

        /// <summary>
        /// Attempts to look up the <see cref="VcpFeature"/> corresponding to a persistence key.
        /// </summary>
        /// <param name="key">Key string previously returned by <see cref="Key"/>.</param>
        /// <param name="feature">
        /// When this method returns <see langword="true"/>, contains the matching feature;
        /// otherwise the default value.
        /// </param>
        /// <returns><see langword="true"/> if the key was recognised; otherwise <see langword="false"/>.</returns>
        public static bool TryParseKey(string key, out VcpFeature feature)
        {
            foreach (var kvp in KeysByFeature)
            {
                if (string.Equals(kvp.Value, key, StringComparison.Ordinal))
                {
                    feature = kvp.Key;
                    return true;
                }
            }

            feature = default;
            return false;
        }
    }
}
