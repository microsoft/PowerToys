// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;

namespace PowerToysExtension.Helpers;

internal sealed class FancyZonesAppliedLayoutEntry
{
    [JsonPropertyName("device")]
    public FancyZonesAppliedDevice Device { get; set; } = new();

    [JsonPropertyName("applied-layout")]
    public FancyZonesAppliedLayout AppliedLayout { get; set; } = new();
}
