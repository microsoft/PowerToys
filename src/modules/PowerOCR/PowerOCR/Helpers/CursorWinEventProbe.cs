// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace PowerOCR.Helpers;

/// <summary>
/// Observes cursor shape notifications. Call Start and Dispose on the overlay UI thread.
/// Notifications arrive asynchronously: any cursor read by the receiver describes callback
/// time, not event time. The event thread identifies the notification source, not a setter stack.
/// </summary>
internal sealed partial class CursorWinEventProbe : IDisposable
{
    private const uint EventObjectNameChange = 0x800C;
    private const int ObjectIdCursor = -9;
    private static readonly ConcurrentDictionary<nint, CursorWinEventProbe> Registrations = new();
    private static readonly WinEventProc WinEventCallback = ObserveEvent;
    private readonly uint _threadId = GetCurrentThreadId();
    private readonly Dictionary<uint, uint> _threadProcesses = new();
    private Action<string>? _recordStatus;
    private Action<EventSample>? _recordEvent;
    private nint _hook;
    private bool _started;
    private bool _disposed;

    internal CursorWinEventProbe(Action<string> recordStatus, Action<EventSample> recordEvent)
    {
        _recordStatus = recordStatus;
        _recordEvent = recordEvent;
    }

    internal readonly record struct EventSample(long CallbackQpc, uint EventTimeMs, uint EventThread, uint EventProcess, uint CallbackThread, nint Hwnd, int ChildId, uint DeliveryDelayMs);

    internal void Start()
    {
        try
        {
            if (_disposed || _started)
            {
                return;
            }

            if (GetCurrentThreadId() != _threadId)
            {
                Report("winevent-start skipped: caller is not the owning UI thread");
                return;
            }

            _started = true;

            // WINEVENT_OUTOFCONTEXT = 0; include this process and all desktop threads.
            _hook = SetWinEventHook(EventObjectNameChange, EventObjectNameChange, 0, WinEventCallback, 0, 0, 0);
            if (_hook != 0)
            {
                Registrations[_hook] = this;
            }

            Report($"winevent-install hook=0x{_hook:X} callbackTid={_threadId} status={(_hook != 0 ? "attached" : "attach-failed")} event=EVENT_OBJECT_NAMECHANGE object=OBJID_CURSOR asynchronous=True");
        }
        catch (Exception ex)
        {
            Report($"winevent-start failed: {ex.GetType().Name}: {ex.Message}");
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
            if (_hook != 0 && (GetCurrentThreadId() != _threadId || UnhookWinEvent(_hook) == 0))
            {
                Report($"winevent-unhook failed hook=0x{_hook:X} ownerTid={_threadId} callerTid={GetCurrentThreadId()}; callback disabled until owning thread exits");
            }
        }
        catch (Exception ex)
        {
            Report($"winevent-unhook failed: {ex.GetType().Name}: {ex.Message}");
        }
        finally
        {
            // The static delegate remains valid even when native removal failed.
            Registrations.TryRemove(_hook, out _);
            _recordEvent = null;
            _recordStatus = null;
        }
    }

    private static void ObserveEvent(nint hook, uint eventId, nint hwnd, int objectId, int childId, uint eventThread, uint eventTimeMs)
    {
        try
        {
            if (eventId != EventObjectNameChange || objectId != ObjectIdCursor || childId != 0 ||
                !Registrations.TryGetValue(hook, out CursorWinEventProbe? probe) || probe._disposed)
            {
                return;
            }

            long callbackQpc = Stopwatch.GetTimestamp();
            uint deliveryDelayMs = unchecked(GetTickCount() - eventTimeMs);
            uint processId = probe.FindEventProcess(eventThread);
            probe._recordEvent?.Invoke(new(callbackQpc, eventTimeMs, eventThread, processId, GetCurrentThreadId(), hwnd, childId, deliveryDelayMs));
        }
        catch (Exception)
        {
            // Diagnostics must not let managed exceptions cross the native callback boundary.
        }
    }

    private uint FindEventProcess(uint threadId)
    {
        if (_threadProcesses.TryGetValue(threadId, out uint processId))
        {
            return processId;
        }

        // Query only ownership; inaccessible or departed threads remain unknown (PID 0).
        nint thread = OpenThread(0x0800, 0, threadId); // THREAD_QUERY_LIMITED_INFORMATION
        try
        {
            processId = thread != 0 ? GetProcessIdOfThread(thread) : 0;
        }
        finally
        {
            if (thread != 0)
            {
                _ = CloseHandle(thread);
            }
        }

        if (_threadProcesses.Count >= 64)
        {
            _threadProcesses.Clear();
        }

        _threadProcesses[threadId] = processId;
        return processId;
    }

    private void Report(string message)
    {
        try
        {
            _recordStatus?.Invoke(message);
        }
        catch (Exception)
        {
            // Reporting cannot affect the overlay or hook lifetime.
        }
    }

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate void WinEventProc(nint hook, uint eventId, nint hwnd, int objectId, int childId, uint eventThread, uint eventTimeMs);

    [DllImport("user32.dll", ExactSpelling = true)]
    private static extern nint SetWinEventHook(uint eventMin, uint eventMax, nint module, WinEventProc callback, uint processId, uint threadId, uint flags);

    [LibraryImport("user32.dll")]
    private static partial int UnhookWinEvent(nint hook);

    [LibraryImport("kernel32.dll")]
    private static partial uint GetCurrentThreadId();

    [LibraryImport("kernel32.dll")]
    private static partial uint GetTickCount();

    [LibraryImport("kernel32.dll")]
    private static partial nint OpenThread(uint desiredAccess, int inheritHandle, uint threadId);

    [LibraryImport("kernel32.dll")]
    private static partial uint GetProcessIdOfThread(nint thread);

    [LibraryImport("kernel32.dll")]
    private static partial int CloseHandle(nint handle);
}
