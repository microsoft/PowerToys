// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace PowerDisplay.Contracts;

public sealed class CliApplyProfileResult
{
    // Response discriminator (see CliResponseHeader): false even on a partial failure — an
    // apply-profile result is a success envelope; the dispatcher reads ExitCode for the outcome.
    public bool IsError { get; init; }

    /// <summary>
    /// The process exit code that reflects the worst outcome across all applied settings.
    /// Precedence: HardwareFailure (5) &gt; InvalidDiscreteValue (3) &gt; OutOfRange (2) &gt; Ok (0).
    /// Defaults to <see cref="CliExitCodes.Ok"/> (0) when all settings applied successfully.
    /// </summary>
    public int ExitCode { get; init; } = CliExitCodes.Ok;

    public string Version { get; init; } = CliSchema.Version;

    public string Command { get; init; } = CliCommandNames.ApplyProfile;

    public string Profile { get; init; } = string.Empty;

    public IReadOnlyList<CliProfileMonitorOutcome> Monitors { get; init; } = [];
}
