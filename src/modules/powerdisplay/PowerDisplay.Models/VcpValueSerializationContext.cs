// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace PowerDisplay.Models
{
    /// <summary>
    /// Source-generated serialization for VCP value restrictions, including Native AOT.
    /// </summary>
    [JsonSourceGenerationOptions(
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never)]
    [JsonSerializable(typeof(VcpValueBlock))]
    [JsonSerializable(typeof(List<VcpValueBlock>))]
    [JsonSerializable(typeof(BuiltInVcpValueBlacklistFile))]
    public partial class VcpValueSerializationContext : JsonSerializerContext
    {
    }
}
