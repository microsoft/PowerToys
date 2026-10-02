// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace MouseJump.Common.Telemetry;

/// <summary>
/// One flattened telemetry record - the result of walking a finished
/// <see cref="System.Diagnostics.Activity"/>'s parent chain and merging every ancestor's tags
/// into a single flat set (see <see cref="TelemetryAdapter"/>), root-most first so a more
/// specific (inner) scope's own value wins on key collision. This is the shape every
/// <see cref="ITelemetryWriter"/> receives - deliberately not tied to any particular output
/// format, so each writer decides that for itself.
/// </summary>
public sealed record TelemetryRecord(
    DateTime Timestamp,
    string Operation,
    string Kind,
    TimeSpan Duration,
    string SpanId,
    string? ParentSpanId,
    IReadOnlyDictionary<string, object?> Properties);
