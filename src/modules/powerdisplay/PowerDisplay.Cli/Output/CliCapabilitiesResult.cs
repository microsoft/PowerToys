// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace PowerDisplay.Cli.Output;

public sealed class CliCapabilitiesResult
{
    public bool Ok { get; init; } = true;

    public string Command { get; init; } = "capabilities";

    public CliMonitorRef Monitor { get; init; } = new();

    public string CommunicationMethod { get; init; } = string.Empty;

    public string? RawCapabilities { get; init; }

    public string? Model { get; init; }

    public string? MccsVersion { get; init; }

    public IReadOnlyList<CliVcpCodeInfo> VcpCodes { get; init; } = [];
}
