// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace PowerDisplay.Common.Models
{
    /// <summary>
    /// VCP feature value information structure.
    /// Represents the current, minimum, and maximum values for a VCP (Virtual Control Panel) feature.
    /// </summary>
    public readonly struct VcpFeatureValue
    {
        /// <summary>
        /// Gets current value
        /// </summary>
        public int Current { get; }

        /// <summary>
        /// Gets minimum value
        /// </summary>
        public int Minimum { get; }

        /// <summary>
        /// Gets maximum value
        /// </summary>
        public int Maximum { get; }

        /// <summary>
        /// Gets a value indicating whether the value information is valid
        /// </summary>
        public bool IsValid { get; }

        /// <summary>
        /// Gets timestamp when the value information was obtained
        /// </summary>
        public DateTime Timestamp { get; }

        public VcpFeatureValue(int current, int minimum, int maximum)
        {
            Current = current;
            Minimum = minimum;
            Maximum = maximum;
            IsValid = current >= minimum && current <= maximum && maximum > minimum;
            Timestamp = DateTime.Now;
        }

        public VcpFeatureValue(int current, int maximum)
            : this(current, 0, maximum)
        {
        }

        /// <summary>
        /// Gets creates invalid value information
        /// </summary>
        public static VcpFeatureValue Invalid => new(-1, -1, -1);

        /// <summary>
        /// Converts value to percentage (0-100)
        /// </summary>
        public int ToPercentage()
        {
            if (!IsValid || Maximum == Minimum)
            {
                return 0;
            }

            return (int)Math.Round((double)(Current - Minimum) * 100 / (Maximum - Minimum));
        }

        /// <summary>
        /// Converts a percentage (0-100) to a raw VCP value within [<paramref name="minimum"/>, <paramref name="maximum"/>].
        /// Symmetric inverse of <see cref="ToPercentage"/>: writers convert the slider percentage to a
        /// device-native value before calling SetVCPFeature, so monitors whose native range isn't 0-100
        /// (e.g. some Samsung displays advertise max=50 for brightness/contrast) receive a value they accept.
        /// </summary>
        /// <param name="percent">Percentage in [0, 100]; values outside the range are clamped.</param>
        /// <param name="maximum">Device-reported maximum raw VCP value.</param>
        /// <param name="minimum">Device-reported minimum raw VCP value (defaults to 0).</param>
        /// <returns>Raw VCP value mapped from the percentage; returns <paramref name="minimum"/> if the range is degenerate.</returns>
        public static int FromPercentage(int percent, int maximum, int minimum = 0)
        {
            if (maximum <= minimum)
            {
                return minimum;
            }

            percent = Math.Clamp(percent, 0, 100);
            return minimum + (int)Math.Round(percent * (double)(maximum - minimum) / 100);
        }

        public override string ToString()
        {
            return IsValid ? $"{Current}/{Maximum} ({ToPercentage()}%)" : "Invalid";
        }
    }
}
