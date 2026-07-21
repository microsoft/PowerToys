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
    }
}
