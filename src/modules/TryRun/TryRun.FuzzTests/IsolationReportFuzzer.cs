// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using PowerToys.TryRun.Core;

namespace PowerToys.TryRun.FuzzTests;

public static class IsolationReportFuzzer
{
    public static void FuzzTarget(ReadOnlySpan<byte> data)
    {
        if (data.Length > 65536)
        {
            return;
        }

        var bytes = data.ToArray();
        try
        {
            var report = IsolationReportParser.ReadDenials(bytes);
            if (report.Events.Count > IsolationReport.MaximumNativeEvents || report.Events.Any(row => row.Resource.Length > 512))
            {
                throw new NotSupportedException("Unbounded denial report.");
            }
        }
        catch (Exception exception) when (IsMalformed(exception))
        {
            // Untrusted or incompatible native report documents fail closed.
        }

        try
        {
            var events = IsolationReportParser.ReadObservations(bytes);
            if (events.Any(row => row.Source != "Workload file (self-reported)"))
            {
                throw new NotSupportedException("Workload file changed evidence provenance.");
            }
        }
        catch (Exception exception) when (IsMalformed(exception))
        {
            // Workload-written files are never treated as authoritative events.
        }
    }

    private static bool IsMalformed(Exception exception) => exception is JsonException or InvalidDataException or InvalidOperationException or KeyNotFoundException or FormatException or ArgumentException;
}
