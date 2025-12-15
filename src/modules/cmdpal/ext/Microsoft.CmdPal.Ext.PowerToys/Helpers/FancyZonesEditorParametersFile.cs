// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace PowerToysExtension.Helpers;

internal sealed class FancyZonesEditorParametersFile
{
    [JsonPropertyName("process-id")]
    public int ProcessId { get; set; }

    [JsonPropertyName("span-zones-across-monitors")]
    public bool SpanZonesAcrossMonitors { get; set; }

    [JsonPropertyName("monitors")]
    public List<FancyZonesEditorMonitor>? Monitors { get; set; }
}
