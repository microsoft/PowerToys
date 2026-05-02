// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;
using PowerToys.MacroCommon.Models;

namespace PowerToys.MacroCommon.Serialization;

// UseStringEnumConverter = true does NOT auto-apply snake_case to enum member names in
// source generation. Every StepType member must carry [JsonStringEnumMemberName("snake_value")]
// explicitly. See StepType.cs.
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    WriteIndented = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    UseStringEnumConverter = true)]
[JsonSerializable(typeof(MacroDefinition))]
[JsonSerializable(typeof(MacroHotkeySettings))]
internal sealed partial class MacroJsonContext : JsonSerializerContext
{
}
