// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.PowerToys.Settings.UI.Library;
using PowerDisplay.Common.Models;

namespace PowerDisplay.Serialization
{
    /// <summary>
    /// JSON source generation context for AOT compatibility.
    /// Eliminates reflection-based JSON serialization.
    /// Note: MonitorStateFile and MonitorStateEntry are now in PowerDisplay.Lib
    /// and should be serialized using ProfileSerializationContext from the Lib.
    /// </summary>
    [JsonSerializable(typeof(IPCMessageAction))]
    [JsonSerializable(typeof(PowerDisplaySettings))]
    [JsonSerializable(typeof(ProfileOperation))]
    [JsonSerializable(typeof(PowerDisplayProfiles))]
    [JsonSerializable(typeof(PowerDisplayProfile))]
    [JsonSerializable(typeof(ProfileMonitorSetting))]

    // MonitorInfo and related types (Settings.UI.Library)
    [JsonSerializable(typeof(MonitorInfo))]
    [JsonSerializable(typeof(VcpCodeDisplayInfo))]
    [JsonSerializable(typeof(VcpValueInfo))]

    // Generic collection types
    [JsonSerializable(typeof(List<string>))]
    [JsonSerializable(typeof(List<MonitorInfo>))]
    [JsonSerializable(typeof(List<VcpCodeDisplayInfo>))]
    [JsonSerializable(typeof(List<VcpValueInfo>))]
    [JsonSerializable(typeof(List<PowerDisplayProfile>))]
    [JsonSerializable(typeof(List<ProfileMonitorSetting>))]

    [JsonSourceGenerationOptions(
        WriteIndented = true,
        PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never)]
    internal sealed partial class AppJsonContext : JsonSerializerContext
    {
    }
}
