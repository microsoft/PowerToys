// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace PowerDisplay.Cli.Output;

public sealed class CliListResult
{
    public bool Ok { get; init; } = true;

    public string Command { get; init; } = "list";

    public IReadOnlyList<CliListMonitor> Monitors { get; init; } = [];
}
