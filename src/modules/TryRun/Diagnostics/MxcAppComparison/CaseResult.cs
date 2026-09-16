// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace PowerToys.TryRun.Diagnostics;

internal sealed record CaseResult(string Name, string Layer, int? ExitCode, bool TimedOut, bool? WindowObserved, string Output, string? CapturePath, string? Error)
{
    public string? ExitHex => ExitCode is { } code ? $"0x{unchecked((uint)code):X8}" : null;
}
