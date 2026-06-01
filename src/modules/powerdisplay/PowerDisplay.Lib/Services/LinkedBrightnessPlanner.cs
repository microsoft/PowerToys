// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Linq;

namespace PowerDisplay.Common.Services
{
    /// <summary>
    /// Pure decision logic for linked brightness, factored out of the WinUI ViewModel so it can be
    /// unit-tested without a DispatcherQueue. Answers three questions for a snapshot of monitors:
    /// which ones the linked master drives, whether there is any broadcast target, and what value
    /// to seed the master slider with when link mode turns on.
    /// </summary>
    public static class LinkedBrightnessPlanner
    {
        /// <summary>
        /// A monitor's state relevant to linked-brightness planning.
        /// </summary>
        /// <param name="Id">Stable <c>Monitor.Id</c> (DevicePath form).</param>
        /// <param name="MonitorNumber">Windows DISPLAY number (1-based); 0 when unknown.</param>
        /// <param name="Brightness">Current brightness percentage (0-100).</param>
        /// <param name="SupportsBrightness">Whether the monitor exposes brightness control.</param>
        /// <param name="Excluded">Whether the user excluded the monitor from linked brightness.</param>
        /// <param name="IsPrimary">Whether the monitor belongs to the Windows primary display source.</param>
        /// <param name="HasValidBrightness">Whether brightness was successfully read from or written to hardware.</param>
        public readonly record struct LinkTarget(
            string Id,
            int MonitorNumber,
            int Brightness,
            bool SupportsBrightness,
            bool Excluded,
            bool IsPrimary,
            bool HasValidBrightness);

        /// <summary>
        /// True when the monitor is driven by the linked master — it supports brightness and the
        /// user has not excluded it.
        /// </summary>
        public static bool IsLinkedTarget(LinkTarget monitor) =>
            monitor.SupportsBrightness && !monitor.Excluded;

        /// <summary>
        /// True when at least one monitor is a linked target, i.e. a master-slider write has
        /// somewhere to go. Initial slider availability additionally requires
        /// <see cref="Seed"/> to find a readable current brightness.
        /// </summary>
        public static bool HasAnyTarget(IEnumerable<LinkTarget> monitors) =>
            monitors.Any(IsLinkedTarget);

        /// <summary>
        /// Count of linked targets — the "N linked" subtitle on the All displays card.
        /// </summary>
        public static int CountTargets(IEnumerable<LinkTarget> monitors) =>
            monitors.Count(IsLinkedTarget);

        /// <summary>
        /// Resolves whether a monitor belongs to the current Windows primary display source.
        /// Returns false when the lookup failed so seed selection falls back to deterministic
        /// display-number ordering.
        /// </summary>
        public static bool ResolveIsPrimary(
            string monitorGdiDeviceName,
            string? currentPrimaryGdiDeviceName) =>
            !string.IsNullOrEmpty(currentPrimaryGdiDeviceName) &&
            string.Equals(
                monitorGdiDeviceName,
                currentPrimaryGdiDeviceName,
                System.StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// The value to seed the master slider with when link mode turns on, or null when there is
        /// no linked target with a valid current brightness (the slider stays unavailable rather
        /// than defaulting to an arbitrary value such as 50%). Prefers the Windows primary display
        /// when it participates in the linked set, then falls back to the lowest Windows DISPLAY
        /// number and Id order for determinism when numbers are missing or tie.
        /// </summary>
        public static int? Seed(IEnumerable<LinkTarget> monitors) =>
            monitors
                .Where(m => IsLinkedTarget(m) && m.HasValidBrightness)
                .OrderByDescending(m => m.IsPrimary)
                .ThenBy(m => m.MonitorNumber <= 0 ? int.MaxValue : m.MonitorNumber)
                .ThenBy(m => m.Id, System.StringComparer.Ordinal)
                .Select(m => (int?)m.Brightness)
                .FirstOrDefault();

        /// <summary>
        /// Profiles are per-monitor snapshots. Applying one while linked brightness is active
        /// should leave linked mode before writing individual monitor values. Otherwise, the
        /// master slider state can diverge from the per-monitor hardware values.
        /// </summary>
        public static bool ShouldDisableLinkedModeBeforeProfileApply(bool linkedLevelsActive) =>
            linkedLevelsActive;
    }
}
