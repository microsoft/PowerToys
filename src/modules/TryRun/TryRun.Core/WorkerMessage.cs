// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace PowerToys.TryRun.Core;

public sealed record WorkerMessage(string Kind, string Text, int? ExitCode = null, bool TimedOut = false)
{
    public const string Output = "output";
    public const string Error = "error";
    public const string Completed = "completed";
    public const string Ready = "ready";
    public const string EnvironmentReady = "environment";
    public const string Isolation = "isolation";
    public const string ProcessStarted = "process-started";

    public WindowsProcessIdentity? Process { get; init; }

    public RunEnvironment? Environment { get; init; }

    public IsolationReport? Report { get; init; }
}
