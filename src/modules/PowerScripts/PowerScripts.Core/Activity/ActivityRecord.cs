// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace PowerScripts.Core.Activity;

public enum ActivityStatus
{
    Running,
    Succeeded,
    Failed,
    Interrupted,
    Unknown,
}

/// <summary>Invocation metadata only. Never add payloads, arguments, paths or diagnostics here.</summary>
public sealed record ActivityRecord
{
    public required Guid Id { get; init; }

    public required string ScriptId { get; init; }

    public required string ScriptName { get; init; }

    public required string ConsumerId { get; init; }

    public required DateTimeOffset StartedUtc { get; init; }

    public DateTimeOffset? FinishedUtc { get; init; }

    public required ActivityStatus Status { get; init; }

    public int? ExitCode { get; init; }

    public required int HostProcessId { get; init; }

    public required long HostStartTimeUtcTicks { get; init; }

    public double? DurationMilliseconds =>
        Status is ActivityStatus.Interrupted or ActivityStatus.Unknown
            ? null
            : Math.Max(0, ((FinishedUtc ?? DateTimeOffset.UtcNow) - StartedUtc).TotalMilliseconds);
}
