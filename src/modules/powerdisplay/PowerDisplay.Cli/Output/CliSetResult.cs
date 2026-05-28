// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace PowerDisplay.Cli.Output;

public sealed class CliSetResult
{
    public bool Ok { get; init; } = true;

    public string Command { get; init; } = "set";

    public CliMonitorRef Monitor { get; init; } = new();

    public string Setting { get; init; } = string.Empty;

    public int? BeforeRaw { get; init; }

    public int AfterRaw { get; init; }

    public string? BeforeDisplay { get; init; }

    public string AfterDisplay { get; init; } = string.Empty;
}
