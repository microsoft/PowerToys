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
        /// <para>
        /// A monitor value without its corresponding <paramref name="readFlag"/> may be cached
        /// rather than live. Such a value must not suppress an absolute restore write, even when
        /// it equals <paramref name="targetValue"/>.
        /// </para>
        /// <para>
        /// <paramref name="displayedValue"/> is the caller's optimistic value — the one the UI is
        /// already showing and will commit once its debounce elapses. A write is suppressed only
        /// when the hardware value is known-equal <em>and</em> the optimistic value agrees;
        /// otherwise a restore issued while a slider commit is still pending would be dropped and
        /// then silently overwritten by that pending commit.
        /// </para>
        /// </remarks>
        /// <param name="targetValue">The value the restore wants the monitor to end at.</param>
        /// <param name="monitor">The monitor whose last-known hardware state is inspected.</param>
        /// <param name="readFlag">Which value to compare.</param>
        /// <param name="displayedValue">The caller's optimistic value for the same setting.</param>
        /// <returns>True when the value must be written to the monitor.</returns>
        public static bool ShouldWrite(
            int targetValue,
            Monitor monitor,
            MonitorReadFlags readFlag,
            int displayedValue)
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

            return (monitor.ReadValues & readFlag) != readFlag
                || targetValue != currentValue
                || targetValue != displayedValue;
        }
    }
}
