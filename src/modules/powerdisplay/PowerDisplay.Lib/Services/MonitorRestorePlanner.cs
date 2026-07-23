// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using PowerDisplay.Common.Models;

namespace PowerDisplay.Common.Services
{
    /// <summary>
    /// Pure decision logic for avoiding redundant monitor restore writes.
    /// </summary>
    public static class MonitorRestorePlanner
    {
        /// <summary>
        /// Determines whether an absolute value should be written to the monitor.
        /// </summary>
        /// <remarks>
        /// A monitor value without its corresponding <paramref name="readFlag"/> may be cached
        /// rather than live. Such a value must not suppress an absolute restore write, even when
        /// it equals <paramref name="targetValue"/>.
        /// </remarks>
        public static bool ShouldWrite(
            int targetValue,
            Monitor monitor,
            MonitorReadFlags readFlag)
        {
            ArgumentNullException.ThrowIfNull(monitor);

            var currentValue = readFlag switch
            {
                MonitorReadFlags.Brightness => monitor.CurrentBrightness,
                MonitorReadFlags.Contrast => monitor.CurrentContrast,
                MonitorReadFlags.Volume => monitor.CurrentVolume,
                MonitorReadFlags.ColorTemperature => monitor.CurrentColorTemperature,
                _ => throw new ArgumentOutOfRangeException(nameof(readFlag), readFlag, "Unsupported restore value flag."),
            };

            return (monitor.ReadValues & readFlag) != readFlag || targetValue != currentValue;
        }
    }
}
