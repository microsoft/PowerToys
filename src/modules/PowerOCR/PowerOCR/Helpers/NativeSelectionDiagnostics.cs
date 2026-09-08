// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

using ManagedCommon;
using PowerOCR.Core.Models;

namespace PowerOCR.Helpers;

/// <summary>
/// Opt-in global cursor sampling for a native selection HWND. The background thread owns
/// the trace buffer and file I/O; Dispose only requests a stop and never waits for it.
/// </summary>
internal sealed partial class NativeSelectionDiagnostics : IDisposable
{
    private const int MaximumEntries = 8000;
    private const int SampleIntervalMilliseconds = 2;
    private readonly nint _hwnd;
    private readonly DisplayBounds _bounds;
    private readonly long _started = Stopwatch.GetTimestamp();
    private readonly Queue<string> _entries = new();
    private readonly List<string> _metadata = new();
    private readonly string _path;
    private readonly nint _cross;
    private readonly nint _arrow;
    private long _drops;
    private int _stopRequested;

    private NativeSelectionDiagnostics(nint hwnd, DisplayBounds bounds)
    {
        _hwnd = hwnd;
        _bounds = bounds;
        _cross = LoadCursor(0, 32515);
        _arrow = LoadCursor(0, 32512);
        DateTime utc = DateTime.UtcNow;
        _path = Path.Combine(Logger.CurrentVersionLogDirectoryPath, $"NativeCursorTrace_{utc:yyyyMMdd_HHmmss_fff}_{Environment.ProcessId}_{hwnd:X}.log");
        uint ownerThread = GetWindowThreadProcessId(hwnd, out uint ownerProcess);
        _metadata.Add(FormattableString.Invariant($"format=1 kind=native-selection utc={utc:O} pid={Environment.ProcessId} native-hwnd=0x{hwnd:X} display={bounds} ownerTid={ownerThread} ownerPid={ownerProcess} qpcStart={_started} qpcFrequency={Stopwatch.Frequency} requestedSampleMs={SampleIntervalMilliseconds} maximumSeconds=60 capacity={MaximumEntries}"));
        _metadata.Add($"app-base {AppContext.BaseDirectory}");
        _metadata.Add($"systemCross=0x{_cross:X} systemArrow=0x{_arrow:X}; labels compare cursor handles, not cursor images.");
        _metadata.Add("ownSurface requires WindowFromPoint/GetAncestor(GA_ROOT) to identify native-hwnd. Other roots are grouped as toolbar-or-other; no toolbar identity is inferred.");
        _metadata.Add("Counters count successful polling samples, not flicker episodes. Shape counters include hidden cursors; visible counters exclude hidden/suppressed cursors. Cursor and window reads are not atomic; short transitions can be missed.");
    }

    internal static NativeSelectionDiagnostics? TryStart(nint hwnd, DisplayBounds bounds)
    {
        NativeSelectionDiagnostics? diagnostics = null;
        try
        {
            if (hwnd == 0 || !File.Exists(Path.Combine(Logger.AppLogDirectoryPath, "cursor-diagnostics.enabled")))
            {
                return null;
            }

            diagnostics = new NativeSelectionDiagnostics(hwnd, bounds);
            new Thread(diagnostics.SampleCursor) { IsBackground = true, Name = "PowerOCR native cursor diagnostics" }.Start();
            return diagnostics;
        }
        catch (Exception ex)
        {
            diagnostics?.Dispose();
            LogFailure("Could not start native PowerOCR cursor diagnostics.", ex);
            return null;
        }
    }

    public void Dispose()
    {
        // This can run in WM_DESTROY: do not perform I/O, wait, or call the window here.
        Interlocked.Exchange(ref _stopRequested, 1);
    }

    private void SampleCursor()
    {
        long samples = 0;
        long failures = 0;
        long ownSurfaceSamples = 0;
        long ownSurfaceCrossSamples = 0;
        long ownSurfaceArrowSamples = 0;
        long ownSurfaceOtherSamples = 0;
        long ownSurfaceVisibleSamples = 0;
        long ownSurfaceVisibleArrowSamples = 0;
        long ownSurfaceVisibleCrossSamples = 0;
        long lastSample = 0;
        long maxGap = 0;
        long lastRecorded = 0;
        CursorInfo previous = default;
        nint previousHwnd = 0;
        nint previousRoot = 0;
        bool previousInside = false;
        string stopReason = "60-second-limit";
        try
        {
            _metadata.Add($"samplerTid={GetCurrentThreadId()}; movement alone is recorded at most every 250 ms, while cursor/visibility/window changes are retained.");
            uint cursorInfoSize = (uint)Marshal.SizeOf<CursorInfo>();
            while (Volatile.Read(ref _stopRequested) == 0 && ElapsedMilliseconds(Stopwatch.GetTimestamp() - _started) < 60000)
            {
                long now = Stopwatch.GetTimestamp();
                if (lastSample != 0)
                {
                    maxGap = Math.Max(maxGap, now - lastSample);
                }

                lastSample = now;
                var info = new CursorInfo { Size = cursorInfoSize };
                if (GetCursorInfo(ref info) != 0)
                {
                    samples++;
                    nint underHwnd = WindowFromPoint(info.Position);
                    nint underRoot = GetAncestor(underHwnd, 2); // GA_ROOT excludes owner windows.
                    bool ownSurface = underRoot == _hwnd;
                    bool inside = info.Position.X >= _bounds.X && info.Position.X < (long)_bounds.X + _bounds.Width
                        && info.Position.Y >= _bounds.Y && info.Position.Y < (long)_bounds.Y + _bounds.Height;
                    bool visible = (info.Flags & 1) != 0 && (info.Flags & 2) == 0; // CURSOR_SHOWING / CURSOR_SUPPRESSED
                    bool isCross = info.Cursor != 0 && info.Cursor == _cross;
                    bool isArrow = !isCross && info.Cursor != 0 && info.Cursor == _arrow;
                    if (ownSurface)
                    {
                        ownSurfaceSamples++;
                        if (isCross)
                        {
                            ownSurfaceCrossSamples++;
                        }
                        else if (isArrow)
                        {
                            ownSurfaceArrowSamples++;
                        }
                        else
                        {
                            ownSurfaceOtherSamples++;
                        }

                        if (visible)
                        {
                            ownSurfaceVisibleSamples++;
                            if (isArrow)
                            {
                                ownSurfaceVisibleArrowSamples++;
                            }
                            else if (isCross)
                            {
                                ownSurfaceVisibleCrossSamples++;
                            }
                        }
                    }

                    bool changed = samples == 1 || info.Cursor != previous.Cursor || info.Flags != previous.Flags
                        || underHwnd != previousHwnd || underRoot != previousRoot || inside != previousInside;
                    if (changed || ElapsedMilliseconds(now - lastRecorded) >= 250)
                    {
                        uint underThread = GetWindowThreadProcessId(underHwnd, out uint underProcess);
                        Record("sample", FormattableString.Invariant($"globalCursor={CursorName(info.Cursor)} visible={visible} flags=0x{info.Flags:X} screen=({info.Position.X},{info.Position.Y}) surface={(ownSurface ? "own-native" : "toolbar-or-other")} ownSurface={ownSurface} insideDisplay={inside} underHwnd=0x{underHwnd:X} underRoot=0x{underRoot:X} underTid={underThread} underPid={underProcess}"), now);
                        lastRecorded = now;
                    }

                    previous = info;
                    previousHwnd = underHwnd;
                    previousRoot = underRoot;
                    previousInside = inside;
                }
                else
                {
                    failures++;
                    if (failures == 1)
                    {
                        Record("sample-error", $"GetCursorInfo error={Marshal.GetLastPInvokeError()}", now);
                    }
                }

                // Preserve the system timer resolution; report the actual worst sampling gap.
                Thread.Sleep(SampleIntervalMilliseconds);
            }
        }
        catch (Exception ex)
        {
            stopReason = "sampler-error";
            Record("sampler-error", ex.ToString(), Stopwatch.GetTimestamp());
        }
        finally
        {
            if (Interlocked.Exchange(ref _stopRequested, 1) != 0 && stopReason != "sampler-error")
            {
                stopReason = "closed";
            }

            string summary = FormattableString.Invariant($"reason={stopReason} samples={samples} failures={failures} ownSurfaceSamples={ownSurfaceSamples} ownSurfaceCrossSamples={ownSurfaceCrossSamples} ownSurfaceArrowSamples={ownSurfaceArrowSamples} ownSurfaceOtherSamples={ownSurfaceOtherSamples} ownSurfaceVisibleSamples={ownSurfaceVisibleSamples} ownSurfaceVisibleArrowSamples={ownSurfaceVisibleArrowSamples} ownSurfaceVisibleCrossSamples={ownSurfaceVisibleCrossSamples} maxSampleGapMs={ElapsedMilliseconds(maxGap):F3} drops={_drops}");
            SaveTrace(summary);
        }
    }

    private string CursorName(nint cursor)
        => $"{(cursor != 0 && cursor == _cross ? "Cross" : cursor != 0 && cursor == _arrow ? "Arrow" : "Other")}(0x{cursor:X})";

    private static double ElapsedMilliseconds(long ticks) => ticks * 1000.0 / Stopwatch.Frequency;

    private string FormatRecord(string category, string details, long timestamp)
        => string.Create(CultureInfo.InvariantCulture, $"tMs={ElapsedMilliseconds(timestamp - _started):F3} qpc={timestamp} {category} {details}");

    private void Record(string category, string details, long timestamp)
    {
        if (_entries.Count == MaximumEntries)
        {
            _entries.Dequeue();
            _drops++;
        }

        _entries.Enqueue(FormatRecord(category, details, timestamp));
    }

    private void SaveTrace(string summary)
    {
        try
        {
            var lines = new List<string>(_metadata);
            lines.Add($"retained={_entries.Count} drops={_drops}; the summary is separate from the bounded sample buffer.");
            lines.AddRange(_entries);
            lines.Add(FormatRecord("summary", summary, Stopwatch.GetTimestamp()));
            File.WriteAllLines(_path, lines);
            Logger.LogInfo($"Native PowerOCR cursor trace saved: {_path}");
        }
        catch (Exception ex)
        {
            LogFailure("Could not save native PowerOCR cursor diagnostics.", ex);
        }
        finally
        {
            _entries.Clear();
        }
    }

    private static void LogFailure(string message, Exception exception)
    {
        try
        {
            Logger.LogError(message, exception);
        }
        catch (Exception)
        {
            // Diagnostic logging must not affect the native window or crash the sampler.
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct CursorInfo
    {
        public uint Size;
        public uint Flags;
        public nint Cursor;
        public NativePoint Position;
    }

    [LibraryImport("user32.dll", SetLastError = true)]
    private static partial int GetCursorInfo(ref CursorInfo info);

    [LibraryImport("user32.dll", EntryPoint = "LoadCursorW")]
    private static partial nint LoadCursor(nint instance, nint name);

    [LibraryImport("user32.dll")]
    private static partial nint WindowFromPoint(NativePoint point);

    [LibraryImport("user32.dll")]
    private static partial nint GetAncestor(nint hwnd, uint flags);

    [LibraryImport("user32.dll")]
    private static partial uint GetWindowThreadProcessId(nint hwnd, out uint processId);

    [LibraryImport("kernel32.dll")]
    private static partial uint GetCurrentThreadId();
}
