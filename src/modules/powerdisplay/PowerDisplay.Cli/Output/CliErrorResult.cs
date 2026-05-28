// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using PowerDisplay.Cli.Errors;

namespace PowerDisplay.Cli.Output;

public sealed class CliErrorResult
{
    public bool Ok { get; init; }

    public string Command { get; init; } = string.Empty;

    public CliError Error { get; init; } = new();

    public CliMonitorRef? Monitor { get; init; }
}
