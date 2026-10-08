// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using Windows.Wdk.System.SystemInformation;
using Windows.Win32.Foundation;
using Windows.Win32.System.WindowsProgramming;
using WdkPInvoke = Windows.Wdk.PInvoke;

namespace CoreWidgetProvider.Helpers;

/// <summary>
/// Finds the processes using the most CPU, the way Task Manager does: one snapshot of every
/// process's CPU time per sample, compared with an earlier snapshot.
/// </summary>
internal sealed class ProcessCpuSampler
{
    private const int InitialBufferSize = 512 * 1024;
    private const uint StatusInfoLengthMismatch = 0xC0000004;

    // CreateTime, UserTime, and KernelTime are in SYSTEM_PROCESS_INFORMATION.Reserved1, after
    // WorkingSetPrivateSize, HardFaultCount, NumberOfThreadsHighWatermark, and CycleTime.
    private const int CreateTimeOffset = 24;
    private const int UserTimeOffset = 32;
    private const int KernelTimeOffset = 40;

    // Windows charges CPU time in clock ticks, so over one second most processes have no time
    // at all and the list would change every sample. Averaging over a few seconds keeps it steady.
    private static readonly TimeSpan Window = TimeSpan.FromSeconds(3);

    // A longer gap means sampling stopped, and an average over the gap wouldn't be current.
    private static readonly TimeSpan MaximumGap = Window * 2;

    private readonly List<Snapshot> _history = [];
    private byte[]? _buffer;

    /// <summary>A process name and its share of all processors' time, in percent.</summary>
    internal readonly record struct ProcessUsage(string Name, float Percent);

    /// <summary>The CPU time of every process at <see cref="Timestamp"/>, a <see cref="Stopwatch"/> timestamp.</summary>
    internal readonly record struct Snapshot(long Timestamp, Dictionary<(ulong ProcessId, long CreateTime), long> Times);

    /// <summary>
    /// Takes a snapshot and returns the <paramref name="count"/> busiest processes over the last
    /// few seconds, with processes of the same name combined. The first call returns nothing.
    /// </summary>
    public unsafe ProcessUsage[] Sample(int count)
    {
        var timestamp = Stopwatch.GetTimestamp();
        PruneHistory(_history, timestamp);
        var baseline = _history.Count > 0 ? _history[0] : default;
        var times = new Dictionary<(ulong ProcessId, long CreateTime), long>(baseline.Times?.Count ?? 0);
        var deltas = new List<(string Name, long Delta)>();

        fixed (byte* buffer = ReadSnapshot())
        {
            var offset = 0u;
            while (true)
            {
                var process = (SYSTEM_PROCESS_INFORMATION*)(buffer + offset);
                var processId = (ulong)(nint)process->UniqueProcessId.Value;
                var reserved = (byte*)&process->Reserved1;

                // Process 0 is the idle process, which isn't a real process.
                if (processId != 0)
                {
                    var key = (processId, *(long*)(reserved + CreateTimeOffset));
                    var time = *(long*)(reserved + UserTimeOffset) + *(long*)(reserved + KernelTimeOffset);
                    times[key] = time;
                    if (baseline.Times is not null && baseline.Times.TryGetValue(key, out var previous) && time > previous)
                    {
                        var name = process->ImageName.Length > 0
                            ? new string(process->ImageName.Buffer.Value, 0, process->ImageName.Length / sizeof(char))
                            : string.Empty;
                        deltas.Add((name, time - previous));
                    }
                }

                if (process->NextEntryOffset == 0)
                {
                    break;
                }

                offset += process->NextEntryOffset;
            }
        }

        // CPU times are in 100-nanosecond units, the same as TimeSpan ticks.
        var elapsed = baseline.Times is null ? 0 : Stopwatch.GetElapsedTime(baseline.Timestamp, timestamp).Ticks;
        _history.Add(new(timestamp, times));
        return Rank(deltas, elapsed, Environment.ProcessorCount, count);
    }

    /// <summary>Forgets the earlier snapshots and frees the snapshot buffer.</summary>
    public void Reset()
    {
        _history.Clear();
        _buffer = null;
    }

    /// <summary>
    /// Drops the snapshots that later samples won't compare with, so the first one left is the
    /// newest that's at least a window older than <paramref name="timestamp"/>, or the oldest
    /// there is. Drops every snapshot if sampling stopped for a while.
    /// </summary>
    internal static void PruneHistory(List<Snapshot> history, long timestamp)
    {
        if (history.Count > 0 && Stopwatch.GetElapsedTime(history[^1].Timestamp, timestamp) > MaximumGap)
        {
            history.Clear();
            return;
        }

        var remove = 0;
        while (remove + 1 < history.Count && Stopwatch.GetElapsedTime(history[remove + 1].Timestamp, timestamp) >= Window)
        {
            remove++;
        }

        history.RemoveRange(0, remove);
    }

    /// <summary>
    /// Combines processes with the same name, converts CPU time to a share of all processors'
    /// time over <paramref name="elapsedTicks"/>, and returns the busiest first.
    /// </summary>
    internal static ProcessUsage[] Rank(IEnumerable<(string Name, long Delta)> deltas, long elapsedTicks, int processorCount, int count)
    {
        if (elapsedTicks <= 0 || processorCount <= 0)
        {
            return [];
        }

        var totals = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        foreach (var (name, delta) in deltas)
        {
            var displayName = name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? name[..^4] : name;
            if (displayName.Length > 0)
            {
                totals[displayName] = totals.GetValueOrDefault(displayName) + delta;
            }
        }

        var ranked = new List<ProcessUsage>(totals.Count);
        var capacity = (double)elapsedTicks * processorCount;
        foreach (var (name, total) in totals)
        {
            ranked.Add(new(name, (float)Math.Clamp(total * 100 / capacity, 0, 100)));
        }

        ranked.Sort(static (left, right) => right.Percent.CompareTo(left.Percent));
        return ranked.Count > count ? ranked.GetRange(0, count).ToArray() : ranked.ToArray();
    }

    // The snapshot holds absolute pointers into the buffer, such as each process's image name, so
    // the buffer must not move between the query and the walk. Pinned arrays never move.
    private static byte[] AllocateBuffer(int size) => GC.AllocateUninitializedArray<byte>(size, pinned: true);

    private unsafe byte[] ReadSnapshot()
    {
        _buffer ??= AllocateBuffer(InitialBufferSize);
        while (true)
        {
            uint needed;
            NTSTATUS status;
            fixed (byte* buffer = _buffer)
            {
                status = WdkPInvoke.NtQuerySystemInformation(SYSTEM_INFORMATION_CLASS.SystemProcessInformation, buffer, (uint)_buffer.Length, &needed);
            }

            if ((uint)status.Value != StatusInfoLengthMismatch)
            {
                if (status.Value < 0)
                {
                    throw new InvalidOperationException($"NtQuerySystemInformation failed with 0x{status.Value:X8}.");
                }

                return _buffer;
            }

            // Processes start between calls, so leave room to grow.
            _buffer = AllocateBuffer(checked((int)Math.Max(needed + (64 * 1024), (uint)_buffer.Length * 2)));
        }
    }
}
