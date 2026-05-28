// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using PowerDisplay.Cli.Errors;
using Monitor = PowerDisplay.Common.Models.Monitor;

namespace PowerDisplay.Cli.Resolution;

/// <summary>
/// Outcome of resolving a CLI selector. Exactly one of <see cref="Monitor"/> and
/// <see cref="Error"/> is non-null. A non-null <see cref="Warning"/> is emitted when
/// the user supplied both <c>-n</c> and <c>-i</c> and the resolver fell back to
/// <c>-i</c>.
/// </summary>
public sealed class MonitorResolution
{
    public Monitor? Monitor { get; init; }

    public CliError? Error { get; init; }

    public string? Warning { get; init; }
}
