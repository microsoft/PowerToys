// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;

namespace PowerDisplay.Cli.Output;

[JsonSourceGenerationOptions(
    WriteIndented = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(CliListResult))]
[JsonSerializable(typeof(CliSetResult))]
[JsonSerializable(typeof(CliGetResult))]
[JsonSerializable(typeof(CliCapabilitiesResult))]
[JsonSerializable(typeof(CliErrorResult))]
public sealed partial class CliJsonContext : JsonSerializerContext
{
}
