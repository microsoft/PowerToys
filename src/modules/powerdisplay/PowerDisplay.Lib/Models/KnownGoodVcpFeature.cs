// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;

namespace PowerDisplay.Common.Models
{
    public sealed class KnownGoodVcpFeature
    {
        [JsonPropertyName("code")]
        public byte Code { get; set; }

        [JsonPropertyName("current")]
        public int Current { get; set; }

        [JsonPropertyName("maximum")]
        public int Maximum { get; set; }

        public KnownGoodVcpFeature Clone() => new()
        {
            Code = Code,
            Current = Current,
            Maximum = Maximum,
        };

        /// <summary>
        /// Builds an observation from a device-native value. The inverse of
        /// <see cref="ToVcpFeatureValue"/>, so the two stay in step.
        /// </summary>
        public static KnownGoodVcpFeature From(byte code, VcpFeatureValue value) => new()
        {
            Code = code,
            Current = value.Current,
            Maximum = value.Maximum,
        };

        public VcpFeatureValue ToVcpFeatureValue() => new(Current, 0, Maximum);
    }
}
