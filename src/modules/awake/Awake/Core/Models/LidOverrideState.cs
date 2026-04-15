// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Text.Json.Serialization;

namespace Awake.Core.Models
{
    /// <summary>
    /// Model for tracking the original lid settings and override state.
    /// Used for crash recovery to restore original settings if Awake exits unexpectedly.
    /// </summary>
    internal sealed class LidOverrideState
    {
        [JsonPropertyName("isOverrideActive")]
        public bool IsOverrideActive { get; set; }

        [JsonPropertyName("originalAcValue")]
        public uint OriginalAcValue { get; set; }

        [JsonPropertyName("originalDcValue")]
        public uint OriginalDcValue { get; set; }

        [JsonPropertyName("schemeGuid")]
        public Guid SchemeGuid { get; set; }
    }
}
