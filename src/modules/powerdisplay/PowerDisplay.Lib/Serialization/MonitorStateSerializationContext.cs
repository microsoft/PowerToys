// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Text.Json.Serialization;
using PowerDisplay.Common.Models;

namespace PowerDisplay.Common.Serialization
{
    /// <summary>
    /// JSON serialization context for MonitorState types.
    /// Provides source-generated serialization for Native AOT compatibility.
    /// Separated from ProfileSerializationContext which moved to PowerDisplay.Models.
    /// </summary>
    [JsonSourceGenerationOptions(
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        IncludeFields = true)]
    [JsonSerializable(typeof(MonitorStateFile))]
    [JsonSerializable(typeof(MonitorStateEntry))]
    [JsonSerializable(typeof(Dictionary<string, MonitorStateEntry>))]
    public partial class MonitorStateSerializationContext : JsonSerializerContext
    {
    }
}
