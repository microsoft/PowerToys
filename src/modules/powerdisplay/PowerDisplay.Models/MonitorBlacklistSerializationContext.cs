// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace PowerDisplay.Models
{
    /// <summary>
    /// JSON serialization context for monitor blacklist types.
    /// Provides source-generated serialization for Native AOT compatibility.
    /// </summary>
    [JsonSourceGenerationOptions(
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        IncludeFields = true)]
    [JsonSerializable(typeof(MonitorBlacklistEntry))]
    [JsonSerializable(typeof(List<MonitorBlacklistEntry>))]
    [JsonSerializable(typeof(BuiltInMonitorBlacklistFile))]
    public partial class MonitorBlacklistSerializationContext : JsonSerializerContext
    {
    }
}
