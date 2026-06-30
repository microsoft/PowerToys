// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using PowerDisplay.Common.Utils;

namespace PowerDisplay.Common.Models
{
    /// <summary>
    /// Per-monitor resolved VCP code for each <see cref="VcpFeature"/>:
    /// a real code (0x00-0xFF), the <see cref="NotSupportedSentinel"/> (checked, no
    /// candidate worked), or absent (not yet resolved). Persisted as
    /// <c>Dictionary&lt;string,int&gt;</c> keyed by <see cref="VcpFeatureRegistry.Key"/>.
    /// </summary>
    public sealed class VcpFeatureCodeMap
    {
        /// <summary>
        /// Value stored for a feature that was checked but has no usable candidate code.
        /// A non-null sentinel is required because the monitor-state file serializes with
        /// <c>WhenWritingNull</c>, which would drop a null and lose the "checked" fact.
        /// </summary>
        public const int NotSupportedSentinel = -1;

        private readonly Dictionary<VcpFeature, int> _codes = new();

        /// <summary>
        /// Returns <see langword="true"/> if <paramref name="feature"/> has been resolved
        /// (supported or explicitly marked not supported); <see langword="false"/> if still
        /// pending resolution.
        /// </summary>
        /// <param name="feature">The feature to check.</param>
        /// <returns><see langword="true"/> when a resolution result has been stored.</returns>
        public bool IsResolved(VcpFeature feature) => _codes.ContainsKey(feature);

        /// <summary>
        /// Returns <see langword="true"/> if <paramref name="feature"/> was resolved to a
        /// usable VCP code (i.e., not <see cref="NotSupportedSentinel"/>).
        /// </summary>
        /// <param name="feature">The feature to check.</param>
        /// <returns><see langword="true"/> when a real code is stored for the feature.</returns>
        public bool IsSupported(VcpFeature feature) =>
            _codes.TryGetValue(feature, out var code) && code != NotSupportedSentinel;

        /// <summary>
        /// Returns the resolved code when supported; otherwise the registry's primary
        /// candidate as a safe default (callers gate writes on <see cref="IsSupported"/>).
        /// </summary>
        /// <param name="feature">The feature whose VCP code is requested.</param>
        /// <returns>
        /// The stored VCP code when <paramref name="feature"/> is supported; otherwise the
        /// first candidate from <see cref="VcpFeatureRegistry.Primary"/>.
        /// </returns>
        public byte GetCode(VcpFeature feature) =>
            _codes.TryGetValue(feature, out var code) && code != NotSupportedSentinel
                ? (byte)code
                : VcpFeatureRegistry.Primary(feature);

        /// <summary>
        /// Records a resolved VCP code for <paramref name="feature"/>.
        /// </summary>
        /// <param name="feature">The feature being resolved.</param>
        /// <param name="code">The VCP code that responded on this monitor.</param>
        public void SetCode(VcpFeature feature, byte code) => _codes[feature] = code;

        /// <summary>
        /// Marks <paramref name="feature"/> as resolved but not supported on this monitor
        /// (no candidate code elicited a valid response).
        /// </summary>
        /// <param name="feature">The feature to mark as not supported.</param>
        public void SetNotSupported(VcpFeature feature) => _codes[feature] = NotSupportedSentinel;

        /// <summary>
        /// Serialises the map to a <c>Dictionary&lt;string,int&gt;</c> suitable for JSON
        /// persistence. Keys are the stable strings from <see cref="VcpFeatureRegistry.Key"/>;
        /// values are byte codes or <see cref="NotSupportedSentinel"/>.
        /// </summary>
        /// <returns>Serialisable dictionary of resolved codes.</returns>
        public Dictionary<string, int> ToPersisted()
        {
            var result = new Dictionary<string, int>(_codes.Count);
            foreach (var kvp in _codes)
            {
                result[VcpFeatureRegistry.Key(kvp.Key)] = kvp.Value;
            }

            return result;
        }

        /// <summary>
        /// Deserialises a <see cref="VcpFeatureCodeMap"/> from a previously persisted
        /// dictionary. Unknown keys are silently ignored; <see langword="null"/> input
        /// returns an empty (unresolved) map. Values outside the valid byte range [0, 255]
        /// that are not the <see cref="NotSupportedSentinel"/> are also ignored (treated as
        /// unresolved) to guard against corrupt or hand-edited state files.
        /// </summary>
        /// <param name="persisted">
        /// Dictionary produced by <see cref="ToPersisted"/>, or <see langword="null"/>.
        /// </param>
        /// <returns>A new <see cref="VcpFeatureCodeMap"/> populated from <paramref name="persisted"/>.</returns>
        public static VcpFeatureCodeMap FromPersisted(Dictionary<string, int>? persisted)
        {
            var map = new VcpFeatureCodeMap();
            if (persisted == null)
            {
                return map;
            }

            foreach (var kvp in persisted)
            {
                if (VcpFeatureRegistry.TryParseKey(kvp.Key, out var feature) &&
                    (kvp.Value == NotSupportedSentinel || (kvp.Value >= 0 && kvp.Value <= 255)))
                {
                    map._codes[feature] = kvp.Value;
                }
            }

            return map;
        }
    }
}
