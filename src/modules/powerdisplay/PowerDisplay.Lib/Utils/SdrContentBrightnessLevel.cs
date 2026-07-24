// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace PowerDisplay.Common.Utils
{
    /// <summary>
    /// Converts between the Windows HDR SDR-white-level fixed-point value and the
    /// 0-100 value shown by the system SDR content brightness slider.
    /// </summary>
    public static class SdrContentBrightnessLevel
    {
        public const uint MinimumRawValue = 1000;
        public const uint MaximumRawValue = 6000;
        private const uint RawUnitsPerPercent = 50;

        public static int FromRaw(uint rawValue)
        {
            var clamped = Math.Clamp(rawValue, MinimumRawValue, MaximumRawValue);
            return (int)((clamped - MinimumRawValue + (RawUnitsPerPercent / 2)) / RawUnitsPerPercent);
        }

        public static uint ToRaw(int percentage)
        {
            var clamped = Math.Clamp(percentage, 0, 100);
            return MinimumRawValue + ((uint)clamped * RawUnitsPerPercent);
        }
    }
}
