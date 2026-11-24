// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace MouseJump.Common.Telemetry;

/// <summary>
/// Persists one already-flattened <see cref="TelemetryRecord"/>. Deliberately synchronous and
/// minimal - <see cref="TelemetryAdapter"/> owns buffering and threading so individual writers
/// don't each need to reimplement that, and it only ever calls <see cref="Write"/> from a single
/// background thread, one record at a time, so implementations don't need their own
/// thread-safety either.
/// </summary>
public interface ITelemetryWriter
{
    void Write(TelemetryRecord record);

    /// <summary>
    /// Flushes anything buffered by the writer itself (e.g. a <see cref="System.IO.StreamWriter"/>'s
    /// internal buffer) out to wherever it ultimately persists - called by
    /// <see cref="TelemetryContext.Flush"/>, after every record enqueued before that call has
    /// already reached <see cref="Write"/>. A no-op for writers that don't buffer anything of
    /// their own.
    /// </summary>
    void Flush();
}
