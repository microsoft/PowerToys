// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace PowerDisplay.Core.Utils
{
    /// <summary>
    /// Utility class for converting between Kelvin color temperature and VCP values.
    /// Centralizes temperature conversion logic to eliminate code duplication (KISS principle).
    /// </summary>
    public static class ColorTemperatureConverter
    {
        /// <summary>
        /// Minimum color temperature in Kelvin (warm)
        /// </summary>
        public const int MinKelvin = 2000;

        /// <summary>
        /// Maximum color temperature in Kelvin (cool)
        /// </summary>
        public const int MaxKelvin = 10000;

        /// <summary>
        /// Convert VCP value to Kelvin temperature
        /// </summary>
        /// <param name="vcpValue">Current VCP value</param>
        /// <param name="vcpMax">Maximum VCP value</param>
        /// <returns>Temperature in Kelvin (2000-10000K)</returns>
        public static int VcpToKelvin(int vcpValue, int vcpMax)
        {
            if (vcpMax <= 0)
            {
                return (MinKelvin + MaxKelvin) / 2; // Default to neutral 6000K
            }

            // Normalize VCP value to 0-1 range
            double normalized = Math.Clamp((double)vcpValue / vcpMax, 0.0, 1.0);

            // Map to Kelvin range
            int kelvin = (int)(MinKelvin + (normalized * (MaxKelvin - MinKelvin)));

            return Math.Clamp(kelvin, MinKelvin, MaxKelvin);
        }

        /// <summary>
        /// Convert Kelvin temperature to VCP value
        /// </summary>
        /// <param name="kelvin">Temperature in Kelvin (2000-10000K)</param>
        /// <param name="vcpMax">Maximum VCP value</param>
        /// <returns>VCP value (0 to vcpMax)</returns>
        public static int KelvinToVcp(int kelvin, int vcpMax)
        {
            // Clamp input to valid range
            kelvin = Math.Clamp(kelvin, MinKelvin, MaxKelvin);

            // Normalize kelvin to 0-1 range
            double normalized = (double)(kelvin - MinKelvin) / (MaxKelvin - MinKelvin);

            // Map to VCP range
            int vcpValue = (int)(normalized * vcpMax);

            return Math.Clamp(vcpValue, 0, vcpMax);
        }

        /// <summary>
        /// Check if a temperature value is in valid Kelvin range
        /// </summary>
        public static bool IsValidKelvin(int kelvin)
        {
            return kelvin >= MinKelvin && kelvin <= MaxKelvin;
        }

        /// <summary>
        /// Get a human-readable description of color temperature
        /// </summary>
        public static string GetTemperatureDescription(int kelvin)
        {
            return kelvin switch
            {
                < 3500 => "Warm",
                < 5500 => "Neutral",
                < 7500 => "Cool",
                _ => "Very Cool"
            };
        }
    }
}
