// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace MouseJump.Common.Telemetry;

/// <summary>
/// Accumulates every <see cref="TelemetryRecord"/> it's given in memory instead of writing it
/// anywhere - for unit tests that want to assert on what a <see cref="TelemetryContext"/>
/// recorded (e.g. that a given operation ran, or how many child spans it had) without a real
/// <see cref="ITelemetryWriter"/>. No locking of its own - same single-threaded-caller guarantee
/// from <see cref="TelemetryAdapter"/> as <see cref="FileTelemetryWriter"/> relies on; call
/// <see cref="TelemetryContext.Flush"/> or <see cref="TelemetryContext.Stop"/> first so every
/// queued record has been drained before reading <see cref="Records"/>.
/// </summary>
public sealed class InMemoryTelemetryWriter : ITelemetryWriter
{
    private readonly List<TelemetryRecord> records = [];

    public IReadOnlyList<TelemetryRecord> Records
        => this.records;

    public void Write(TelemetryRecord record)
        => this.records.Add(record);

    public void Flush()
    {
    }
}
