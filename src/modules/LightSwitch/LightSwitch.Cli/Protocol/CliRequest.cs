// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace LightSwitch.Cli.Protocol;

internal sealed class CliRequest
{
    public int Version { get; init; } = CliProtocol.Version;

    public string Command { get; init; } = string.Empty;

    public string? Mode { get; init; }
}
