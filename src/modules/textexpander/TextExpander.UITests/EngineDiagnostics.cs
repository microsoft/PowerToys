// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.PowerToys.UITest.Next;

namespace Microsoft.PowerToys.TextExpander.UITests;

/// <summary>
/// What a failed expansion is reported with: the engine's own counters, which it writes to diagnostics.txt when asked
/// (as <c>PowerToys.TextExpander.exe --diagnostics-file</c> asks it), how busy the CPU was, and any winapp.exe still running.
/// </summary>
internal static class EngineDiagnostics
{
    private const string RequestMessageName = "PowerToys.TextExpander.ShowDiagnostics";

    public static string FilePath => Path.Combine(TextExpanderFiles.Folder, "diagnostics.txt");

    /// <summary>Gets the engine's counters on one line, such as the snippets loaded and how its last insertion ended.</summary>
    public static string Read(int timeoutMS = 5_000)
    {
        var window = FindWindowW(TextExpanderTestBase.EngineMessageWindowClass, null);
        if (window == IntPtr.Zero)
        {
            return "<the engine's window isn't there>";
        }

        byte[]? earlier = File.Exists(FilePath) ? TextExpanderFiles.ReadAllBytesShared(FilePath) : null;
        var writtenBefore = earlier is null ? DateTime.MinValue : File.GetLastWriteTimeUtc(FilePath);
        if (!PostMessageW(window, RegisterWindowMessageW(RequestMessageName), new IntPtr(1), IntPtr.Zero))
        {
            return $"<the request didn't reach the engine: error {Marshal.GetLastPInvokeError()}>";
        }

        try
        {
            var written = WaitHelper.WaitForStable(
                () => File.Exists(FilePath) && File.GetLastWriteTimeUtc(FilePath) > writtenBefore ? File.ReadAllText(FilePath) : null,
                text => !string.IsNullOrEmpty(text),
                timeoutMS,
                pollIntervalMS: 100,
                shouldRetryException: ex => ex is IOException or UnauthorizedAccessException);
            return written.Succeeded
                ? string.Join(", ", written.LastObservation!.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                : $"<the engine didn't write {FilePath} within {timeoutMS} ms>";
        }
        finally
        {
            // It's the user's file, if there was one.
            try
            {
                if (earlier is null)
                {
                    File.Delete(FilePath);
                }
                else
                {
                    File.WriteAllBytes(FilePath, earlier);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
            }
        }
    }

    /// <summary>Gets the CPU time counters, to compare with a later sample.</summary>
    public static (long Idle, long Total, long Tick) SampleCpu()
    {
        _ = GetSystemTimes(out var idle, out var kernel, out var user);
        return (idle, kernel + user, Environment.TickCount64);
    }

    /// <summary>Describes how busy the CPU was since <paramref name="since"/>; kernel time includes idle time.</summary>
    public static string DescribeCpu((long Idle, long Total, long Tick) since)
    {
        var now = SampleCpu();
        var total = now.Total - since.Total;
        return total <= 0
            ? "CPU load unknown"
            : $"CPU {100 - (100 * (now.Idle - since.Idle) / total)}% busy over {(now.Tick - since.Tick) / 1000.0:0.0} s on {Environment.ProcessorCount} processor(s)";
    }

    /// <summary>Lists the winapp.exe processes still running: one left over from a slow call competes for the CPU.</summary>
    public static string DescribeWinapp()
    {
        var processes = Process.GetProcessesByName("winapp");
        try
        {
            return processes.Length == 0
                ? "no winapp.exe running"
                : "winapp.exe running: " + string.Join(", ", processes.Select(Describe));
        }
        finally
        {
            foreach (var process in processes)
            {
                process.Dispose();
            }
        }

        static string Describe(Process process)
        {
            try
            {
                return $"process {process.Id} ({process.TotalProcessorTime.TotalSeconds:0.0} s of CPU)";
            }
            catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
            {
                return $"process {process.Id}";
            }
        }
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr FindWindowW(string className, string? windowName);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern uint RegisterWindowMessageW(string name);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PostMessageW(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSystemTimes(out long idleTime, out long kernelTime, out long userTime);
}
