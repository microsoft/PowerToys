// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;

namespace LightSwitch.Cli.Protocol;

internal sealed class CliResponse
{
    [JsonRequired]
    public int Version { get; init; } = CliProtocol.Version;

    [JsonRequired]
    public bool Success { get; init; }

    public CliState? State { get; init; }

    public CliError? Error { get; init; }
}
