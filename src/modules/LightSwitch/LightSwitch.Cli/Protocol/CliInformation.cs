// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace LightSwitch.Cli.Protocol;

// Help/version are produced locally, never sent over the service pipe. Keeping this separate
// from CliResponse makes their additional JSON fields explicit without changing the IPC schema.
internal sealed class CliInformation
{
    public int Version { get; init; } = CliProtocol.Version;

    public bool Success { get; init; } = true;

    public string? Help { get; init; }

    public string? CliVersion { get; init; }
}
