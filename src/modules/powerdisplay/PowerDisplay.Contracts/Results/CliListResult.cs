// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace PowerDisplay.Contracts;

public sealed class CliListResult
{
    // Response discriminator (see CliResponseHeader): false on success DTOs, true only on CliErrorResult.
    public bool IsError { get; init; }

    public bool Ok { get; init; } = true;

    public string Version { get; init; } = CliSchema.Version;

    public string Command { get; init; } = "list";

    public IReadOnlyList<CliListMonitor> Monitors { get; init; } = [];
}
