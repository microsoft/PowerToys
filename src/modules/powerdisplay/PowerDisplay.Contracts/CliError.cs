// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace PowerDisplay.Contracts;

/// <summary>
/// Structured CLI error returned by validators and commands. Mapped 1:1 to the JSON
/// <c>error</c> envelope. <see cref="ExitCode"/> is derived from <see cref="Code"/> via
/// <see cref="CliExitCodes.ForErrorCode"/>, so the two can never disagree; callers set only
/// <see cref="Code"/>.
/// </summary>
public sealed class CliError
{
    public string Code { get; init; } = string.Empty;

    public string Message { get; init; } = string.Empty;

    /// <summary>Process exit code for this error, derived from <see cref="Code"/>. Serialized for
    /// JSON consumers; recomputed from <see cref="Code"/> on deserialization.</summary>
    public int ExitCode => CliExitCodes.ForErrorCode(Code);

    public string? Setting { get; init; }

    public string? Requested { get; init; }

    public string? ExpectedRange { get; init; }

    public IReadOnlyList<CliSupportedValue>? Supported { get; init; }

    public string? Hint { get; init; }
}
