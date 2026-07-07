// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
using System.Text.Json.Serialization;

namespace PowerDisplay.Contracts;

[JsonSourceGenerationOptions(
    WriteIndented = false,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(CliRequestEnvelope))]
[JsonSerializable(typeof(ApplyProfileRequest))]
[JsonSerializable(typeof(CliListResult))]
[JsonSerializable(typeof(CliGetResult))]
[JsonSerializable(typeof(CliSetResult))]
[JsonSerializable(typeof(CliCapabilitiesResult))]
[JsonSerializable(typeof(CliProfileListResult))]
[JsonSerializable(typeof(CliProfileInfo))]
[JsonSerializable(typeof(CliApplyProfileResult))]
[JsonSerializable(typeof(CliErrorResult))]
[JsonSerializable(typeof(CliResponseHeader))]
public sealed partial class ContractsJsonContext : System.Text.Json.Serialization.JsonSerializerContext
{
}
