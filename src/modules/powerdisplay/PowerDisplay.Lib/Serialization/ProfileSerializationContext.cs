// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Text.Json.Serialization;
using PowerDisplay.Common.Models;

namespace PowerDisplay.Common.Serialization
{
    /// <summary>
    /// JSON serialization context for PowerDisplay Profile types.
    /// Provides source-generated serialization for Native AOT compatibility.
    /// </summary>
    [JsonSourceGenerationOptions(
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        IncludeFields = true)]

    // Profile Types
    [JsonSerializable(typeof(ProfileMonitorSetting))]
    [JsonSerializable(typeof(List<ProfileMonitorSetting>))]
    [JsonSerializable(typeof(PowerDisplayProfile))]
    [JsonSerializable(typeof(List<PowerDisplayProfile>))]
    [JsonSerializable(typeof(PowerDisplayProfiles))]
    [JsonSerializable(typeof(ProfileOperation))]
    [JsonSerializable(typeof(List<ProfileOperation>))]
    [JsonSerializable(typeof(ColorTemperatureOperation))]
    [JsonSerializable(typeof(List<ColorTemperatureOperation>))]
    public partial class ProfileSerializationContext : JsonSerializerContext
    {
    }
}
