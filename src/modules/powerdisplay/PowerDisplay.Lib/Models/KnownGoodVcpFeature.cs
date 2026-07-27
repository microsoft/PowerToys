// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Text.Json.Serialization;

namespace PowerDisplay.Common.Models
{
    public enum VcpObservationSource
    {
        MaximumCompatibilityProbe,
        CapabilitiesInitialization,
    }

    public sealed class KnownGoodVcpFeature
    {
        [JsonPropertyName("code")]
        public byte Code { get; set; }

        [JsonPropertyName("current")]
        public int Current { get; set; }

        [JsonPropertyName("maximum")]
        public int Maximum { get; set; }

        [JsonPropertyName("source")]
        [JsonConverter(typeof(JsonStringEnumConverter<VcpObservationSource>))]
        public VcpObservationSource Source { get; set; }

        [JsonPropertyName("lastSuccessfulUtc")]
        public DateTime LastSuccessfulUtc { get; set; }

        public KnownGoodVcpFeature Clone() => new()
        {
            Code = Code,
            Current = Current,
            Maximum = Maximum,
            Source = Source,
            LastSuccessfulUtc = LastSuccessfulUtc,
        };

        public VcpFeatureValue ToVcpFeatureValue() => new(Current, 0, Maximum);

        /// <summary>
        /// Returns whether this observation is still young enough to stand in for a live read.
        /// </summary>
        /// <remarks>
        /// A cache entry is the only evidence that survives a capabilities string which omits a
        /// code, so nothing in discovery can contradict it. A monitor can lose DDC/CI support for a
        /// code while keeping the same DevicePath (OSD toggle, firmware update, GPU or cable
        /// change); without an age bound one successful observation would advertise that code for
        /// the life of the entry. Every successful read restamps
        /// <see cref="LastSuccessfulUtc"/>, so an entry backing a feature that still works never
        /// expires. A <paramref name="utcNow"/> earlier than the stamp (clock moved backwards)
        /// yields a negative age and is treated as fresh.
        /// </remarks>
        /// <param name="utcNow">Current UTC time.</param>
        /// <param name="maxAge">Maximum age an observation may reach before it must be re-proven.</param>
        public bool IsFresh(DateTime utcNow, TimeSpan maxAge) => utcNow - LastSuccessfulUtc <= maxAge;
    }
}
