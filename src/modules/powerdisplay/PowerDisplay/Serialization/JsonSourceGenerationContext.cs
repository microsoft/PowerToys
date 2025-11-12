// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.PowerToys.Settings.UI.Library;

#pragma warning disable SA1402 // File may only contain a single type - Related JSON serialization types grouped together

namespace PowerDisplay.Serialization
{
    /// <summary>
    /// JSON source generation context for AOT compatibility.
    /// Eliminates reflection-based JSON serialization.
    /// </summary>
    [JsonSerializable(typeof(PowerDisplayMonitorsIPCResponse))]
    [JsonSerializable(typeof(MonitorInfoData))]
    [JsonSerializable(typeof(IPCMessageAction))]
    [JsonSerializable(typeof(MonitorStateFile))]
    [JsonSerializable(typeof(MonitorStateEntry))]
    [JsonSerializable(typeof(PowerDisplaySettings))]
    [JsonSourceGenerationOptions(
        WriteIndented = true,
        PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never)]
    internal sealed partial class AppJsonContext : JsonSerializerContext
    {
    }

    /// <summary>
    /// IPC message wrapper for parsing action-based messages.
    /// Used in App.xaml.cs for dynamic IPC command handling.
    /// </summary>
    internal sealed class IPCMessageAction
    {
        [JsonPropertyName("action")]
        public string? Action { get; set; }
    }

    /// <summary>
    /// Monitor state file structure for JSON persistence.
    /// Made internal (from private) to support source generation.
    /// </summary>
    internal sealed class MonitorStateFile
    {
        [JsonPropertyName("monitors")]
        public Dictionary<string, MonitorStateEntry> Monitors { get; set; } = new();

        [JsonPropertyName("lastUpdated")]
        public DateTime LastUpdated { get; set; }
    }

    /// <summary>
    /// Individual monitor state entry.
    /// Made internal (from private) to support source generation.
    /// </summary>
    internal sealed class MonitorStateEntry
    {
        [JsonPropertyName("brightness")]
        public int Brightness { get; set; }

        [JsonPropertyName("colorTemperature")]
        public int ColorTemperature { get; set; }

        [JsonPropertyName("contrast")]
        public int Contrast { get; set; }

        [JsonPropertyName("volume")]
        public int Volume { get; set; }

        [JsonPropertyName("lastUpdated")]
        public DateTime LastUpdated { get; set; }
    }
}
