// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace PowerToys.TryRun.Core;

public sealed record IsolationReport(string CaptureStatus, string CaptureDetail, IReadOnlyList<IsolationEvent> Events)
{
    public const int MaximumNativeEvents = 100;

    public string EvidenceNote => "Configured restrictions describe the requested policy. Workload observations are self-reported. Missing events do not prove that no access was attempted.";
}
