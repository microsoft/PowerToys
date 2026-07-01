// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PowerToys.UITest.Next;

/// <summary>
/// Static helpers for discovering and attaching to windows that aren't the test's primary scope.
/// </summary>
/// <remarks>
/// Most tests target one module's main window (handled by <see cref="UITestBase"/> + <see cref="SessionHelper"/>).
/// But scenarios like "send the ColorPicker hotkey and assert the Editor pops up" need to discover
/// a brand-new window that may not exist when the test starts. These helpers wrap
/// <c>winapp ui list-windows --json</c> to find/wait for those windows by process or title.
/// </remarks>
public static class WindowsFinder
{
    public sealed record WindowInfo(long Hwnd, string Title, string ProcessName, int ProcessId, string ClassName, int Width, int Height);

    /// <summary>List all UIA-visible windows.</summary>
    /// <remarks>
    /// NOTE: winappcli's unfiltered <c>list-windows --json</c> currently omits windows that have
    /// no Win32 title (e.g. the ColorPicker editor exposes its name only via UIA Name, not the
    /// HWND title). Use <see cref="ListByApp"/> with a process/PID filter when you need to see
    /// those — winappcli returns them in the filtered form.
    /// </remarks>
    public static IReadOnlyList<WindowInfo> ListAll() => Parse(WinappCli.Invoke("ui", "list-windows", "--json"));

    /// <summary>
    /// List UIA-visible windows belonging to <paramref name="appNameOrPid"/> (process name substring or PID).
    /// Uses winappcli's <c>-a</c> filter, which works around the bug where unfiltered
    /// <c>list-windows</c> drops windows without a Win32 title.
    /// </summary>
    public static IReadOnlyList<WindowInfo> ListByApp(string appNameOrPid) =>
        Parse(WinappCli.Invoke("ui", "list-windows", "-a", appNameOrPid, "--json"));

    private static IReadOnlyList<WindowInfo> Parse(WinappCli.Result r)
    {
        if (!r.Success || string.IsNullOrEmpty(r.StdOut))
        {
            return Array.Empty<WindowInfo>();
        }

        try
        {
            using var doc = JsonDocument.Parse(r.StdOut);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
            {
                return Array.Empty<WindowInfo>();
            }

            var list = new List<WindowInfo>();
            foreach (var w in doc.RootElement.EnumerateArray())
            {
                list.Add(new WindowInfo(
                    Hwnd: w.TryGetProperty("hwnd", out var h) && h.ValueKind == JsonValueKind.Number ? h.GetInt64() : 0,
                    Title: w.TryGetProperty("title", out var t) ? (t.GetString() ?? string.Empty) : string.Empty,
                    ProcessName: w.TryGetProperty("processName", out var pn) ? (pn.GetString() ?? string.Empty) : string.Empty,
                    ProcessId: w.TryGetProperty("processId", out var pid) && pid.ValueKind == JsonValueKind.Number ? pid.GetInt32() : 0,
                    ClassName: w.TryGetProperty("className", out var cn) ? (cn.GetString() ?? string.Empty) : string.Empty,
                    Width: w.TryGetProperty("width", out var ww) && ww.ValueKind == JsonValueKind.Number ? ww.GetInt32() : 0,
                    Height: w.TryGetProperty("height", out var hh) && hh.ValueKind == JsonValueKind.Number ? hh.GetInt32() : 0));
            }

            return list;
        }
        catch
        {
            return Array.Empty<WindowInfo>();
        }
    }

    /// <summary>
    /// Poll until a window matching <paramref name="predicate"/> appears, or <paramref name="timeoutMS"/>
    /// elapses. Returns the window's <see cref="Session"/> wrapper on success.
    /// </summary>
    public static Session? WaitForWindow(Func<WindowInfo, bool> predicate, PowerToysModule attributeAs = PowerToysModule.Runner, int timeoutMS = 10_000, int pollIntervalMS = 250)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromMilliseconds(timeoutMS);
        while (DateTime.UtcNow < deadline)
        {
            foreach (var w in ListAll())
            {
                Debug.WriteLine(w.ToString());
                if (predicate(w))
                {
                    return new Session(attributeAs, w.Hwnd, w.Title, w.ProcessId, w.ProcessName);
                }
            }

            Thread.Sleep(pollIntervalMS);
        }

        return null;
    }

    /// <summary>Convenience wrapper: wait for a window with the given title substring.</summary>
    public static Session? WaitForWindowByTitle(string titleContains, int timeoutMS = 10_000)
        => WaitForWindow(w => w.Title.Contains(titleContains, StringComparison.OrdinalIgnoreCase), timeoutMS: timeoutMS);

    /// <summary>
    /// Wait for any window owned by a process whose name contains <paramref name="processNameContains"/>.
    /// Uses winappcli's <c>-a</c> filter under the hood so untitled windows (e.g. the ColorPicker
    /// editor) are discoverable — the unfiltered <c>list-windows</c> drops those.
    /// </summary>
    public static Session? WaitForWindowByProcess(string processNameContains, int timeoutMS = 10_000, int pollIntervalMS = 250)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromMilliseconds(timeoutMS);
        while (DateTime.UtcNow < deadline)
        {
            foreach (var w in ListByApp(processNameContains))
            {
                Debug.WriteLine(w.ToString());
                return new Session(PowerToysModule.Runner, w.Hwnd, w.Title, w.ProcessId, w.ProcessName);
            }

            Thread.Sleep(pollIntervalMS);
        }

        return null;
    }

    /// <summary>
    /// Same as <see cref="WaitForWindowByProcess"/> but filters with <paramref name="predicate"/>.
    /// Use when the same process owns multiple windows (e.g. ColorPickerUI exposes both the
    /// small picker overlay and the larger editor window).
    /// </summary>
    public static Session? WaitForWindowByApp(
        string appNameOrPid,
        Func<WindowInfo, bool> predicate,
        int timeoutMS = 10_000,
        int pollIntervalMS = 250)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromMilliseconds(timeoutMS);
        while (DateTime.UtcNow < deadline)
        {
            foreach (var w in ListByApp(appNameOrPid))
            {
                Debug.WriteLine(w.ToString());
                if (predicate(w))
                {
                    return new Session(PowerToysModule.Runner, w.Hwnd, w.Title, w.ProcessId, w.ProcessName);
                }
            }

            Thread.Sleep(pollIntervalMS);
        }

        return null;
    }
}
