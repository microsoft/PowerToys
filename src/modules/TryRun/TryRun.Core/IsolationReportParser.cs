// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;

namespace PowerToys.TryRun.Core;

public static class IsolationReportParser
{
    public const int MaximumDocumentBytes = 4 * 1024 * 1024;
    public const int MaximumObservationBytes = 32768;

    public static IsolationReport ReadDenials(ReadOnlyMemory<byte> json)
    {
        if (json.Length > MaximumDocumentBytes)
        {
            throw new InvalidDataException("MXC's denial document exceeds the 4 MiB display limit.");
        }

        using var document = JsonDocument.Parse(WithoutBom(json), new JsonDocumentOptions { MaxDepth = 16 });
        var root = document.RootElement;
        var denials = root.GetProperty("denials");
        var summary = root.GetProperty("summary");
        var count = denials.GetArrayLength();
        var total = summary.GetProperty("totalDenials").GetInt32();
        var truncated = summary.GetProperty("deniedResourcesTruncated").GetBoolean();
        if (count > 10000 || total != count)
        {
            throw new InvalidDataException("MXC's denial document has inconsistent counts.");
        }

        var events = new List<IsolationEvent>();
        foreach (var denial in denials.EnumerateArray().Take(IsolationReport.MaximumNativeEvents))
        {
            var resource = Text(denial.GetProperty("resource"));
            var type = Text(denial.GetProperty("resourceType"));
            var access = Text(denial.GetProperty("accessType"));
            events.Add(new IsolationEvent("MXC denial capture (block)", resource, access, "Blocked", $"Resource type: {type}. Recorded by MXC while deny-by-default remained enabled."));
        }

        var limited = truncated || count > events.Count;
        return new IsolationReport(
            limited ? "Partial" : "Complete",
            $"MXC recorded {total} unique denials; displaying {events.Count}." + (limited ? " The capture or displayed list is truncated." : string.Empty) + (total == 0 ? " No recorded denial is not proof of unrestricted or safe behavior." : string.Empty),
            events);
    }

    public static IReadOnlyList<IsolationEvent> ReadObservations(ReadOnlyMemory<byte> json)
    {
        if (json.Length > MaximumObservationBytes)
        {
            throw new InvalidDataException("The workload observation file exceeds 32 KiB.");
        }

        using var document = JsonDocument.Parse(WithoutBom(json), new JsonDocumentOptions { MaxDepth = 8 });
        var root = document.RootElement;
        if (root.GetProperty("version").GetInt32() != 1)
        {
            throw new InvalidDataException("Unsupported workload observation format.");
        }

        var observations = root.GetProperty("observations");
        if (observations.GetArrayLength() > 20)
        {
            throw new InvalidDataException("A workload observation file may contain at most 20 rows.");
        }

        return observations.EnumerateArray().Select(row => new IsolationEvent(
            "Workload file (self-reported)",
            Text(row.GetProperty("resource")),
            Text(row.GetProperty("access")),
            "Reported: " + Text(row.GetProperty("outcome")),
            Text(row.GetProperty("detail")))).ToArray();
    }

    private static string Text(JsonElement value)
    {
        var text = value.GetString() ?? throw new InvalidDataException("Missing observation text.");
        return new string(text.Take(512).Select(character => char.IsControl(character) ? ' ' : character).ToArray());
    }

    private static ReadOnlyMemory<byte> WithoutBom(ReadOnlyMemory<byte> json) => json.Span.StartsWith(new byte[] { 0xEF, 0xBB, 0xBF }) ? json[3..] : json;
}
