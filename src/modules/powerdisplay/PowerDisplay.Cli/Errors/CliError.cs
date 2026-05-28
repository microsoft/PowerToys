// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace PowerDisplay.Cli.Errors;

/// <summary>
/// Structured CLI error returned by validators and commands. Mapped 1:1 to the JSON
/// <c>error</c> envelope. The exit code is selected by the command runner from
/// <see cref="ExitCode"/>.
/// </summary>
public sealed class CliError
{
    public string Code { get; init; } = string.Empty;

    public string Message { get; init; } = string.Empty;

    public int ExitCode { get; init; }

    public string? Setting { get; init; }

    public string? Requested { get; init; }

    public string? ExpectedRange { get; init; }

    public IReadOnlyList<CliSupportedValue>? Supported { get; init; }

    public string? Hint { get; init; }
}
