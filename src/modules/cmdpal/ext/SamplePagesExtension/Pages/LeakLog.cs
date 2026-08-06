// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace SamplePagesExtension.Pages;

/// <summary>
/// An in-memory history of measurements and the actions between them, so a run
/// can be read back as a sequence rather than a single snapshot.
/// </summary>
/// <remarks>
/// Bounded rather than an ever-growing <see cref="StringBuilder"/> - a diagnostic
/// for tracking leaks should not be one. Entries are kept as separate strings and
/// joined on demand, which also keeps trimming cheap.
/// </remarks>
internal static class LeakLog
{
    private const int MaxEntries = 500;

    private static readonly Lock Gate = new();
    private static readonly Queue<string> Entries = new();

    public static int Count
    {
        get
        {
            lock (Gate)
            {
                return Entries.Count;
            }
        }
    }

    /// <summary>
    /// Records an action - something the user or the host did, as opposed to a
    /// measurement of the result.
    /// </summary>
    public static void RecordAction(string action) => Append($"* {action}");

    /// <summary>
    /// Records a measurement snapshot.
    /// </summary>
    public static void RecordSnapshot(string summary) => Append($"  {summary}");

    public static string Dump()
    {
        lock (Gate)
        {
            if (Entries.Count == 0)
            {
                return "No history recorded yet.";
            }

            var builder = new StringBuilder(Entries.Count * 96);
            foreach (var entry in Entries)
            {
                builder.AppendLine(entry);
            }

            return builder.ToString();
        }
    }

    public static void Clear()
    {
        lock (Gate)
        {
            Entries.Clear();
        }
    }

    private static void Append(string line)
    {
        var stamped = $"{DateTime.Now:HH:mm:ss.fff}  {line}";

        lock (Gate)
        {
            Entries.Enqueue(stamped);

            while (Entries.Count > MaxEntries)
            {
                Entries.Dequeue();
            }
        }
    }
}
