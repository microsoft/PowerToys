// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Text.Json.Serialization;

using EnvironmentVariablesUILib.Models;

namespace EnvironmentVariablesUILib.Helpers
{
    [JsonSourceGenerationOptions(WriteIndented = true)]
    [JsonSerializable(typeof(List<ProfileVariablesSet>))]
    [JsonSerializable(typeof(IEnumerable<ProfileVariablesSet>))]
    internal sealed partial class EnvironmentVariablesJsonContext : JsonSerializerContext
    {
    }
}
