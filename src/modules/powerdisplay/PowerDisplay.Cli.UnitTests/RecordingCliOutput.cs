// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using PowerDisplay.Cli.Output;
using PowerDisplay.Contracts;

namespace PowerDisplay.Cli.UnitTests;

/// <summary>
/// Shared <see cref="ICliOutput"/> test double. Records each rendered result as a tagged stdout line
/// and each warning/error as a tagged stderr line (so dispatch tests can assert which renderer ran),
/// and additionally tracks the error count and last error (so batch tests can assert aggregation).
/// </summary>
internal sealed class RecordingCliOutput : ICliOutput
{
    private readonly List<string> stdoutLines = new();

    private readonly List<string> stderrLines = new();

    public IReadOnlyList<string> StdoutLines => this.stdoutLines;

    public IReadOnlyList<string> StderrLines => this.stderrLines;

    public int ErrorCount { get; private set; }

    public CliErrorResult? LastError { get; private set; }

    public void WriteListResult(CliListResult r) => this.stdoutLines.Add("list:" + r.Command);

    public void WriteSetResult(CliSetResult r) => this.stdoutLines.Add("set:" + r.Setting);

    public void WriteGetResult(CliGetResult r) => this.stdoutLines.Add("get");

    public void WriteCapabilitiesResult(CliCapabilitiesResult r) => this.stdoutLines.Add("capabilities");

    public void WriteProfileListResult(CliProfileListResult r) => this.stdoutLines.Add("profiles");

    public void WriteApplyProfileResult(CliApplyProfileResult r) => this.stdoutLines.Add("apply-profile:" + r.Profile);

    public void WriteError(CliErrorResult r)
    {
        this.ErrorCount++;
        this.LastError = r;
        this.stderrLines.Add("error:" + r.Error.Code + ":" + r.Error.ExitCode);
    }

    public void WriteWarning(string message) => this.stderrLines.Add("warn:" + message);
}
