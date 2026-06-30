// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using PowerDisplay.Common.Models;

namespace PowerDisplay.Common.Utils
{
    /// <summary>
    /// Resolves each <see cref="VcpFeature"/> to a concrete VCP code for one monitor:
    /// cap-string-first, probe-as-fallback. Pure and side-effect free — the only I/O is
    /// the caller-supplied <paramref name="probe"/> delegate, which must be a read-only
    /// VCP GET that returns true only when the code yields a usable value.
    /// </summary>
    public static class VcpFeatureResolver
    {
        /// <summary>
        /// Resolves every <see cref="VcpFeature"/> to a VCP code (or not-supported sentinel)
        /// for a single monitor, using cap-string-first then probe-as-fallback strategy.
        /// <para>
        /// Algorithm per feature:
        /// <list type="number">
        ///   <item>If <paramref name="persisted"/> already has a resolved decision (code or
        ///     sentinel), reuse it verbatim without touching <paramref name="caps"/> or
        ///     <paramref name="probe"/>.</item>
        ///   <item>Phase 1 (both modes): walk the priority-ordered candidates from
        ///     <see cref="VcpFeatureRegistry.Candidates"/>; pick the first code the cap
        ///     string reports as supported.</item>
        ///   <item>Phase 2 (<paramref name="maxCompatibilityMode"/> only, fresh discovery
        ///     when <paramref name="persisted"/> is <see langword="null"/>, and Phase 1 found
        ///     nothing): call <paramref name="probe"/> on each candidate in priority order;
        ///     stop at the first that returns <see langword="true"/>. Skipped when
        ///     <paramref name="persisted"/> is non-<see langword="null"/> (cap-string refresh
        ///     mode — probing is expensive and not appropriate for refreshes).</item>
        ///   <item>If nothing resolves, store <see cref="VcpFeatureCodeMap.NotSupportedSentinel"/>.</item>
        /// </list>
        /// </para>
        /// </summary>
        /// <param name="caps">Cap string data for the monitor.</param>
        /// <param name="maxCompatibilityMode">
        /// When <see langword="true"/>, Phase 2 probe fallback is enabled for features not
        /// found in the cap string.
        /// </param>
        /// <param name="persisted">
        /// Previously resolved map to reuse, or <see langword="null"/> for a fresh resolution.
        /// Only features already resolved in this map are reused; missing features are resolved
        /// fresh.
        /// </param>
        /// <param name="probe">
        /// Caller-supplied read-only VCP GET delegate. Invoked only in Phase 2. Must return
        /// <see langword="true"/> when the given code elicits a usable value from the monitor.
        /// </param>
        /// <returns>A fully resolved <see cref="VcpFeatureCodeMap"/> covering all features.</returns>
        public static VcpFeatureCodeMap Resolve(
            VcpCapabilities caps,
            bool maxCompatibilityMode,
            VcpFeatureCodeMap? persisted,
            Func<byte, bool> probe)
        {
            var map = new VcpFeatureCodeMap();

            foreach (var feature in VcpFeatureRegistry.AllFeatures)
            {
                // Per-feature reuse: a persisted decision (code or sentinel) wins verbatim,
                // and is neither re-derived from caps nor re-probed.
                if (persisted != null && persisted.IsResolved(feature))
                {
                    if (persisted.IsSupported(feature))
                    {
                        map.SetCode(feature, persisted.GetCode(feature));
                    }
                    else
                    {
                        map.SetNotSupported(feature);
                    }

                    continue;
                }

                var candidates = VcpFeatureRegistry.Candidates(feature);
                byte? resolved = null;

                // Phase 1 (both modes): first candidate the cap string reports as supported.
                foreach (var code in candidates)
                {
                    if (caps.SupportsVcpCode(code))
                    {
                        resolved = code;
                        break;
                    }
                }

                // Phase 2 (max-compat only, fresh discovery, when Phase 1 found nothing): probe in priority order.
                // When a persisted map is supplied the caller is doing a cap-string refresh, not a
                // first-time discovery; probing is expensive/disruptive and is therefore skipped.
                if (resolved == null && maxCompatibilityMode && persisted == null)
                {
                    foreach (var code in candidates)
                    {
                        if (probe(code))
                        {
                            resolved = code;
                            break;
                        }
                    }
                }

                if (resolved.HasValue)
                {
                    map.SetCode(feature, resolved.Value);
                }
                else
                {
                    map.SetNotSupported(feature);
                }
            }

            return map;
        }
    }
}
