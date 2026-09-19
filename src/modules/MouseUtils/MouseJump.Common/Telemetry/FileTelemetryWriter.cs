// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;

using MouseJump.Models;

namespace MouseJump.Common.Telemetry;

/// <summary>
/// Writes each <see cref="TelemetryRecord"/> as one JSON Lines record to a file. No buffering,
/// locking, or async machinery of its own - <see cref="TelemetryAdapter"/> guarantees
/// <see cref="Write"/> is only ever called from a single background thread, one record at a
/// time. Not auto-flushed per line - relies on the normal <see cref="StreamWriter"/> buffer and
/// flushes on <see cref="Dispose"/>, trading a small risk of losing the last few buffered lines
/// on a hard crash for not paying flush cost on every single record.
/// </summary>
public sealed class FileTelemetryWriter : ITelemetryWriter, IDisposable
{
    /// <summary>
    /// <see cref="Write"/> serializes each record's properties as a loosely-typed
    /// <see cref="Dictionary{TKey, TValue}"/> of <see cref="object"/> values, so
    /// <see cref="System.Text.Json"/> resolves a converter from each value's own runtime type -
    /// unlike a strongly-typed model, a <c>[JsonConverter]</c> attribute on some property
    /// somewhere else (e.g. <c>ScreenInfo.Handle</c>) never gets consulted here. Win32 handles
    /// (<c>HMONITOR</c>, <c>HWND</c>, ...) are <see cref="nint"/> under the hood and turn up
    /// fairly often in telemetry properties (e.g. <c>new { handle = screenInfo.Handle }</c>) -
    /// without this, serializing one throws <see cref="NotSupportedException"/> (nint isn't
    /// natively supported), which previously went unnoticed because nothing ever flushed the
    /// writer far enough to actually hit it.
    /// </summary>
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        Converters = { new NintJsonConverter() },
    };

    private readonly StreamWriter writer;

    public FileTelemetryWriter(string filePath)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            _ = Directory.CreateDirectory(directory);
        }

        this.writer = new StreamWriter(filePath, append: true);
    }

    public void Write(TelemetryRecord record)
    {
        var fields = new Dictionary<string, object?>(record.Properties)
        {
            ["timestamp"] = record.Timestamp,
            ["operation"] = record.Operation,
            ["kind"] = record.Kind,
            ["durationMs"] = record.Duration.TotalMilliseconds,
            ["spanId"] = record.SpanId,
        };

        if (record.ParentSpanId is not null)
        {
            fields["parentSpanId"] = record.ParentSpanId;
        }

        this.writer.WriteLine(JsonSerializer.Serialize(fields, FileTelemetryWriter.SerializerOptions));
    }

    public void Flush()
        => this.writer.Flush();

    public void Dispose()
        => this.writer.Dispose();
}
