// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace PowerOCR.Helpers;

/// <summary>
/// Observes native cursor dispatch without changing message handling. Call Refresh, CheckHealth,
/// and Dispose on the overlay UI thread. Other threads' windows cannot be subclassed by this probe.
/// </summary>
internal sealed partial class CursorMessageProbe : IDisposable
{
    private const uint WmNcDestroy = 0x0082;
    private static readonly ConcurrentDictionary<(nint Hwnd, nuint Id), CursorMessageProbe> Registrations = new();
    private static readonly SubclassProc SubclassCallback = ObserveMessage;
    private static long _nextId;
    private static int _membershipQueryUnavailable;
    private readonly nint _rootHwnd;
    private readonly uint _threadId = GetCurrentThreadId();
    private readonly nuint _id = (nuint)Interlocked.Increment(ref _nextId);
    private readonly uint _healthMessage = RegisterWindowMessage("PowerOCR.CursorDiagnostics.Probe.v2");
    private readonly HashSet<nint> _observed = new();
    private readonly HashSet<nint> _attached = new();
    private readonly Dictionary<nint, nint> _initialWindowProcedures = new();
    private readonly EnumWindowsProc _enumerateWindow;
    private Action<string>? _recordWindow;
    private Action<MessageSample>? _recordMessage;
    private nint _heartbeatSequence;
    private nint _heartbeatHwnd;
    private nint _heartbeatToken;
    private bool _heartbeatReceived;
    private bool _checkingHealth;
    private bool _disposed;

    internal CursorMessageProbe(nint rootHwnd, Action<string> recordWindow, Action<MessageSample> recordMessage)
    {
        _rootHwnd = rootHwnd;
        _recordWindow = recordWindow;
        _recordMessage = recordMessage;
        _enumerateWindow = ObserveWindow;
    }

    internal readonly record struct MessageSample(long Started, long Finished, nint Hwnd, uint Message, nuint WParam, nint LParam, nint Before, nint After, nint Result);

    internal void Refresh()
    {
        try
        {
            if (_disposed || _rootHwnd == 0)
            {
                return;
            }

            if (GetCurrentThreadId() != _threadId)
            {
                Report("probe-refresh skipped: caller is not the owning UI thread");
                return;
            }

            ObserveWindow(_rootHwnd, 0);
            _ = EnumChildWindows(_rootHwnd, _enumerateWindow, 0);
        }
        catch (Exception ex)
        {
            Report($"probe-refresh failed: {ex.GetType().Name}: {ex.Message}");
        }
    }

    internal void CheckHealth(string reason)
    {
        try
        {
            if (_disposed || _checkingHealth)
            {
                return;
            }

            if (GetCurrentThreadId() != _threadId)
            {
                Report($"native-probe-health reason={reason} skipped: caller is not the owning UI thread");
                return;
            }

            _checkingHealth = true;
            try
            {
                foreach (nint hwnd in new List<nint>(_attached))
                {
                    CheckWindowHealth(hwnd, reason);
                }
            }
            finally
            {
                _checkingHealth = false;
            }
        }
        catch (Exception ex)
        {
            Report($"native-probe-health reason={reason} failed: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private void CheckWindowHealth(nint hwnd, string reason)
    {
        try
        {
            uint ownerThread = GetWindowThreadProcessId(hwnd, out uint ownerProcess);
            if (_disposed || ownerThread != _threadId || !_attached.Contains(hwnd))
            {
                Report($"native-probe-health reason={reason} hwnd=0x{hwnd:X} tid={ownerThread} pid={ownerProcess} skipped-invalid-or-other-thread");
                return;
            }

            string membership = ReadMembership(hwnd);
            nint currentProcedure = GetWindowLongPtr(hwnd, -4); // GWLP_WNDPROC
            _initialWindowProcedures.TryGetValue(hwnd, out nint initialProcedure);
            _heartbeatHwnd = hwnd;
            _heartbeatToken = ++_heartbeatSequence;
            _heartbeatReceived = false;
            nint result = 0;
            try
            {
                if (_healthMessage != 0)
                {
                    // The token is an integer, never a pointer. Same-thread SendMessage
                    // returns after the native chain runs; no subclass is reinstalled.
                    result = SendMessage(hwnd, _healthMessage, _id, _heartbeatToken);
                }

                Report($"native-probe-health qpc={Stopwatch.GetTimestamp()} reason={reason} hwnd=0x{hwnd:X} membership={membership} initialWndproc=0x{initialProcedure:X} currentWndproc=0x{currentProcedure:X} wndprocChanged={initialProcedure != currentProcedure} heartbeatMessage=0x{_healthMessage:X} heartbeatToken={_heartbeatToken} heartbeatReceived={_heartbeatReceived} heartbeatResult=0x{result:X}");
            }
            finally
            {
                _heartbeatHwnd = 0;
                _heartbeatToken = 0;
            }
        }
        catch (Exception ex)
        {
            Report($"native-probe-health reason={reason} hwnd=0x{hwnd:X} failed: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private string ReadMembership(nint hwnd)
    {
        if (Volatile.Read(ref _membershipQueryUnavailable) != 0)
        {
            return "unavailable";
        }

        try
        {
            return GetWindowSubclass(hwnd, SubclassCallback, _id, out _) != 0 ? "True" : "False";
        }
        catch (Exception ex) when (ex is EntryPointNotFoundException or DllNotFoundException)
        {
            // Some comctl32 versions do not export this optional query by name.
            // Keep the independent WNDPROC and heartbeat checks running without retrying it.
            if (Interlocked.Exchange(ref _membershipQueryUnavailable, 1) == 0)
            {
                Report($"native-probe-membership status=unavailable query=GetWindowSubclass disabledForProcess=True error={ex.GetType().Name}: {ex.Message}");
            }

            return "unavailable";
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        try
        {
            if (GetCurrentThreadId() == _threadId)
            {
                foreach (nint hwnd in new List<nint>(_attached))
                {
                    Detach(hwnd, destroying: false);
                }
            }
            else
            {
                Report("probe-dispose skipped: caller is not the owning UI thread; registrations retained until window destruction");
            }
        }
        catch (Exception ex)
        {
            Report($"probe-dispose failed: {ex.GetType().Name}: {ex.Message}");
        }
        finally
        {
            _recordMessage = null;
            _recordWindow = null;
        }
    }

    private unsafe int ObserveWindow(nint hwnd, nint parameter)
    {
        try
        {
            if (_disposed || !_observed.Add(hwnd))
            {
                return 1;
            }

            uint ownerThread = GetWindowThreadProcessId(hwnd, out uint ownerProcess);
            char* buffer = stackalloc char[256];
            int length = GetClassName(hwnd, buffer, 256);
            string className = length > 0 ? new string(buffer, 0, length) : "<unknown>";

            // PowerOCR supports x64 and ARM64, where this is the exported pointer-sized API.
            nint classCursor = GetClassLongPtr(hwnd, -12); // GCLP_HCURSOR
            string status = "skipped-other-thread";
            if (ownerThread == _threadId)
            {
                Registrations[(hwnd, _id)] = this;
                _attached.Add(hwnd);
                bool installed = false;
                try
                {
                    installed = SetWindowSubclass(hwnd, SubclassCallback, _id, 0) != 0;
                    status = installed ? "attached" : "attach-failed";
                    if (installed)
                    {
                        // Compare later checks with the procedure after our subclass was installed.
                        _initialWindowProcedures[hwnd] = GetWindowLongPtr(hwnd, -4); // GWLP_WNDPROC
                    }
                }
                finally
                {
                    if (!installed)
                    {
                        _attached.Remove(hwnd);
                        Registrations.TryRemove((hwnd, _id), out _);
                    }
                }
            }

            Report($"native-window hwnd=0x{hwnd:X} root=0x{_rootHwnd:X} class={className} tid={ownerThread} pid={ownerProcess} classCursor=0x{classCursor:X} probeTid={_threadId} subclass={status}");
        }
        catch (Exception ex)
        {
            Report($"native-window hwnd=0x{hwnd:X} observation failed: {ex.GetType().Name}: {ex.Message}");
        }

        return 1;
    }

    private static nint ObserveMessage(nint hwnd, uint message, nuint wParam, nint lParam, nuint id, nuint referenceData)
    {
        CursorMessageProbe? probe = null;
        long started = 0;
        nint before = 0;
        bool sample = false;
        try
        {
            if (Registrations.TryGetValue((hwnd, id), out probe) && !probe._disposed)
            {
                if (probe._healthMessage != 0 && message == probe._healthMessage && wParam == id
                    && hwnd == probe._heartbeatHwnd && lParam == probe._heartbeatToken)
                {
                    probe._heartbeatReceived = true;
                }

                if (IsInteresting(message))
                {
                    started = Stopwatch.GetTimestamp();
                    before = GetCursor();
                    sample = true;
                }
            }

            if (message == WmNcDestroy)
            {
                probe?.Detach(hwnd, destroying: true);
            }
        }
        catch (Exception)
        {
            // An observer failure must never prevent forwarding the original message.
        }

        nint result;
        try
        {
            result = DefSubclassProc(hwnd, message, wParam, lParam);
        }
        catch (Exception)
        {
            // Never allow a managed exception across the unmanaged callback boundary.
            return 0;
        }

        try
        {
            if (sample && probe is not null && !probe._disposed)
            {
                nint after = GetCursor();
                long finished = Stopwatch.GetTimestamp();
                probe._recordMessage?.Invoke(new(started, finished, hwnd, message, wParam, lParam, before, after, result));
            }
        }
        catch (Exception)
        {
            // Diagnostics cannot alter the original window procedure's return value.
        }

        return result;
    }

    private static bool IsInteresting(uint message) => message is
        0x0020 or // WM_SETCURSOR
        0x0084 or // WM_NCHITTEST
        0x0200 or // WM_MOUSEMOVE
        0x00A0 or // WM_NCMOUSEMOVE
        0x0245 or 0x0249 or 0x024A or // WM_POINTERUPDATE / ENTER / LEAVE
        0x0006 or 0x0007 or 0x0008 or // WM_ACTIVATE / SETFOCUS / KILLFOCUS
        0x0215 or 0x02A3; // WM_CAPTURECHANGED / MOUSELEAVE

    private void Detach(nint hwnd, bool destroying)
    {
        bool removed = false;
        try
        {
            removed = RemoveWindowSubclass(hwnd, SubclassCallback, _id) != 0;
            if (!removed && !destroying)
            {
                Report($"probe-remove failed hwnd=0x{hwnd:X}; registration retained until window destruction");
            }
        }
        finally
        {
            if (removed || destroying)
            {
                _attached.Remove(hwnd);
                _observed.Remove(hwnd);
                _initialWindowProcedures.Remove(hwnd);
                Registrations.TryRemove((hwnd, _id), out _);
            }
        }

        // The static delegate remains rooted even after a failed removal during destruction.
        // Any later callback with no registration simply forwards the message.
    }

    private void Report(string message)
    {
        try
        {
            _recordWindow?.Invoke(message);
        }
        catch (Exception)
        {
            // Reporting must not affect the overlay or escape an enumeration callback.
        }
    }

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate nint SubclassProc(nint hwnd, uint message, nuint wParam, nint lParam, nuint id, nuint referenceData);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate int EnumWindowsProc(nint hwnd, nint parameter);

    [DllImport("comctl32.dll", ExactSpelling = true)]
    private static extern int SetWindowSubclass(nint hwnd, SubclassProc callback, nuint id, nuint referenceData);

    [DllImport("comctl32.dll", ExactSpelling = true)]
    private static extern int RemoveWindowSubclass(nint hwnd, SubclassProc callback, nuint id);

    [DllImport("comctl32.dll", ExactSpelling = true)]
    private static extern int GetWindowSubclass(nint hwnd, SubclassProc callback, nuint id, out nuint referenceData);

    [DllImport("user32.dll", ExactSpelling = true)]
    private static extern int EnumChildWindows(nint hwnd, EnumWindowsProc callback, nint parameter);

    [LibraryImport("comctl32.dll")]
    private static partial nint DefSubclassProc(nint hwnd, uint message, nuint wParam, nint lParam);

    [LibraryImport("user32.dll")]
    private static partial nint GetCursor();

    [LibraryImport("kernel32.dll")]
    private static partial uint GetCurrentThreadId();

    [LibraryImport("user32.dll")]
    private static partial uint GetWindowThreadProcessId(nint hwnd, out uint processId);

    [LibraryImport("user32.dll", EntryPoint = "GetClassLongPtrW")]
    private static partial nint GetClassLongPtr(nint hwnd, int index);

    [LibraryImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static partial nint GetWindowLongPtr(nint hwnd, int index);

    [LibraryImport("user32.dll", EntryPoint = "RegisterWindowMessageW", StringMarshalling = StringMarshalling.Utf16)]
    private static partial uint RegisterWindowMessage(string message);

    [LibraryImport("user32.dll", EntryPoint = "SendMessageW")]
    private static partial nint SendMessage(nint hwnd, uint message, nuint wParam, nint lParam);

    [LibraryImport("user32.dll", EntryPoint = "GetClassNameW")]
    private static unsafe partial int GetClassName(nint hwnd, char* className, int maxCount);
}
