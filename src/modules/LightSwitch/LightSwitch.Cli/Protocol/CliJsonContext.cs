// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;

namespace LightSwitch.Cli.Protocol;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(CliRequest))]
[JsonSerializable(typeof(CliResponse))]
[JsonSerializable(typeof(CliInformation))]
internal sealed partial class CliJsonContext : JsonSerializerContext
{
}
