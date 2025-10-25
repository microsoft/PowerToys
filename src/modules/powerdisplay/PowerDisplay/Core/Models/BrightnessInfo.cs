// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace PowerDisplay.Core.Models
{
    /// <summary>
    /// Brightness information structure
    /// </summary>
    public readonly struct BrightnessInfo
    {
        /// <summary>
        /// Current brightness value
        /// </summary>
        public int Current { get; }

        /// <summary>
        /// Minimum brightness value
        /// </summary>
        public int Minimum { get; }

        /// <summary>
        /// Maximum brightness value
        /// </summary>
        public int Maximum { get; }

        /// <summary>
        /// Whether the brightness information is valid
        /// </summary>
        public bool IsValid { get; }

        /// <summary>
        /// Timestamp when the brightness information was obtained
        /// </summary>
        public DateTime Timestamp { get; }

        public BrightnessInfo(int current, int minimum, int maximum)
        {
            Current = current;
            Minimum = minimum;
            Maximum = maximum;
            IsValid = current >= minimum && current <= maximum && maximum > minimum;
            Timestamp = DateTime.Now;
        }

        public BrightnessInfo(int current, int maximum)
            : this(current, 0, maximum)
        {
        }

        /// <summary>
        /// Creates invalid brightness information
        /// </summary>
        public static BrightnessInfo Invalid => new(-1, -1, -1);

        /// <summary>
        /// Converts brightness value to percentage (0-100)
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
        /// Creates brightness value from percentage
        /// </summary>
        public int FromPercentage(int percentage)
        {
            if (!IsValid)
            {
                return -1;
            }

            percentage = Math.Clamp(percentage, 0, 100);
            return Minimum + (int)Math.Round((double)(Maximum - Minimum) * percentage / 100);
        }

        public override string ToString()
        {
            return IsValid ? $"{Current}/{Maximum} ({ToPercentage()}%)" : "Invalid";
        }
    }
}
