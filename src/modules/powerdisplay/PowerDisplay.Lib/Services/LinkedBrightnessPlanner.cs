// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Linq;

namespace PowerDisplay.Common.Services
{
    /// <summary>
    /// Pure decision logic for linked brightness, factored out of the WinUI ViewModel so initial
    /// seed behavior can be unit-tested without a DispatcherQueue.
    /// </summary>
    public static class LinkedBrightnessPlanner
    {
        /// <summary>
        /// A monitor's state relevant to linked-brightness planning.
        /// </summary>
        /// <param name="Id">Stable <c>Monitor.Id</c> (DevicePath form).</param>
        /// <param name="MonitorNumber">Windows DISPLAY number (1-based); 0 when unknown.</param>
        /// <param name="Brightness">Current brightness percentage (0-100).</param>
        public readonly record struct LinkTarget(
            string Id,
            int MonitorNumber,
            int Brightness);

        /// <summary>
        /// The value to seed the master slider with when link mode turns on, or null when there is
        /// no linked target. The caller passes only included brightness-capable targets. Prefers
        /// the lowest Windows DISPLAY number and Id order for determinism when numbers are missing
        /// or tie.
        /// </summary>
        public static int? Seed(IEnumerable<LinkTarget> linkedTargets) =>
            linkedTargets
                .OrderBy(m => m.MonitorNumber <= 0 ? int.MaxValue : m.MonitorNumber)
                .ThenBy(m => m.Id, System.StringComparer.Ordinal)
                .Select(m => (int?)m.Brightness)
                .FirstOrDefault();
    }
}
