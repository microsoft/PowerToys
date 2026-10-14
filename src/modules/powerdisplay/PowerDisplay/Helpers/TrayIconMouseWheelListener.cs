// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using ManagedCommon;
using PowerDisplay.Common.Services;

namespace PowerDisplay.Helpers
{
    internal sealed partial class TrayIconMouseWheelListener : IDisposable
    {
        private const int WhMouseLl = 14;
        private const int HcAction = 0;
        private const uint WmMouseMove = 0x0200;
        private const uint WmMouseWheel = 0x020A;
        private const uint WmApp = 0x8000;
        private const uint WmArm = WmApp + 1;
        private const uint WmDisarm = WmApp + 2;
        private const uint WmDrainSamples = WmApp + 3;
        private const uint WmShutdown = WmApp + 4;
        private const uint PmNoRemove = 0;
        private const int MaxQueuedSamples = 32;

        // Non-zero LRESULT from a WH_MOUSE_LL proc blocks the message for the rest of the hook
        // chain and for the target window.
        private const nint HookHandled = 1;

        private readonly Action<TrayWheelSample[]> _sampleBatchHandler;
        private readonly Action<long> _disarmedHandler;
        private readonly ManualResetEventSlim _ready = new();
        private readonly object _pendingStateLock = new();
        private readonly Queue<TrayWheelSample> _samples = new(MaxQueuedSamples);
        private readonly Thread _thread;

        private LowLevelMouseProc? _hookProc;
        private uint _threadId;
        private nint _hookHandle;
        private volatile bool _armed;
        private int _drainSamplesPosted;
        private bool _hookInstallFailureLogged;
        private bool _hookReleaseFailureLogged;
        private bool _postFailureLogged;
        private volatile bool _threadStopped;
        private TrayIconBounds _pendingBounds;
        private long _pendingGeneration;
        private TrayIconBounds _activeBounds;
        private long _activeGeneration;
        private int _disposed;

        public TrayIconMouseWheelListener(
            Action<TrayWheelSample[]> sampleBatchHandler,
            Action<long> disarmedHandler)
        {
            ArgumentNullException.ThrowIfNull(sampleBatchHandler);
            ArgumentNullException.ThrowIfNull(disarmedHandler);

            _sampleBatchHandler = sampleBatchHandler;
            _disarmedHandler = disarmedHandler;
            _thread = new Thread(ThreadMain)
            {
                IsBackground = true,
                Name = "PowerDisplay.TrayMouseWheel",
            };
            _thread.Start();

            if (!_ready.Wait(TimeSpan.FromSeconds(5)))
            {
                // The thread has not published its id yet, so WmShutdown has nowhere to go. Latch
                // the request instead; ThreadMain checks it before entering the message loop.
                Volatile.Write(ref _disposed, 1);
                throw new InvalidOperationException("Timed out starting the tray mouse-wheel thread.");
            }
        }

        /// <summary>
        /// Gets a value indicating whether the low-level hook is armed for the current hover.
        /// </summary>
        public bool IsArmed => _armed;

        public void Arm(TrayIconBounds bounds, long hoverGeneration)
        {
            if (Volatile.Read(ref _disposed) != 0 || !bounds.IsValid)
            {
                return;
            }

            lock (_pendingStateLock)
            {
                _pendingBounds = bounds;
                _pendingGeneration = hoverGeneration;
            }

            PostCommand(WmArm, 0);
        }

        public void Disarm()
        {
            if (Volatile.Read(ref _disposed) == 0)
            {
                PostCommand(WmDisarm, 0);
            }
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
            {
                return;
            }

            if (!PostThreadMessageNative(_threadId, WmShutdown, 0, 0))
            {
                Logger.LogWarning(
                    $"[TrayWheel] Failed to request hook thread shutdown with error {Marshal.GetLastPInvokeError()}");
            }

            if (!_thread.Join(TimeSpan.FromSeconds(5)))
            {
                Logger.LogError("[TrayWheel] Timed out stopping the hook thread");
            }

            _ready.Dispose();
            GC.SuppressFinalize(this);
        }

        private void ThreadMain()
        {
            _hookProc = HookCallback;
            _ = PeekMessageNative(out _, 0, 0, 0, PmNoRemove);
            _threadId = GetCurrentThreadIdNative();
            _ready.Set();

            if (Volatile.Read(ref _disposed) != 0)
            {
                return;
            }

            try
            {
                RunMessageLoop();
            }
            catch (Exception ex)
            {
                // Nothing sits above this thread to contain a failure - the WinUI unhandled
                // exception handler only covers the UI thread - so an escaping exception would end
                // the process. Wheel control is optional: log it, drop the hook in the finally
                // below, and let the thread retire.
                Logger.LogError($"[TrayWheel] Hook thread stopped after an unhandled failure: {ex.Message}");
            }
            finally
            {
                DisarmCore(notify: false);
                _threadStopped = true;
            }
        }

        private void RunMessageLoop()
        {
            // This thread owns no windows, so the only messages it can retrieve are the commands
            // posted to it here and WM_QUIT. There is nothing to translate or dispatch: the hook
            // proc is called by the system during message retrieval, not through DispatchMessage.
            var running = true;
            while (running)
            {
                var result = GetMessageNative(out var message, 0, 0, 0);
                if (result == 0)
                {
                    break;
                }

                if (result < 0)
                {
                    Logger.LogError(
                        $"[TrayWheel] GetMessage failed with error {Marshal.GetLastPInvokeError()}");
                    break;
                }

                switch (message.Message)
                {
                    case WmArm:
                        HandleArm();
                        break;
                    case WmDisarm:
                        DisarmCore(notify: true);
                        break;
                    case WmDrainSamples:
                        DrainSamples();
                        break;
                    case WmShutdown:
                        running = false;
                        break;
                }
            }
        }

        private void HandleArm()
        {
            lock (_pendingStateLock)
            {
                _activeBounds = _pendingBounds;
                _activeGeneration = _pendingGeneration;
            }

            _armed = _activeBounds.IsValid && _activeGeneration != 0;
            if (_armed && !EnsureHook())
            {
                DisarmCore(notify: true);
            }
        }

        private bool EnsureHook()
        {
            if (_hookHandle != 0)
            {
                return true;
            }

            var hookPointer = Marshal.GetFunctionPointerForDelegate(_hookProc!);
            _hookHandle = SetWindowsHookExNative(
                WhMouseLl,
                hookPointer,
                GetModuleHandleNative(null),
                0);

            if (_hookHandle != 0)
            {
                _hookInstallFailureLogged = false;
                return true;
            }

            if (!_hookInstallFailureLogged)
            {
                Logger.LogWarning(
                    $"[TrayWheel] SetWindowsHookEx failed with error {Marshal.GetLastPInvokeError()}");
                _hookInstallFailureLogged = true;
            }

            return false;
        }

        private void DisarmCore(bool notify)
        {
            var generation = _activeGeneration;
            _armed = false;
            _activeGeneration = 0;
            _activeBounds = default;
            _samples.Clear();
            Interlocked.Exchange(ref _drainSamplesPosted, 0);

            if (_hookHandle != 0)
            {
                var hook = _hookHandle;
                _hookHandle = 0;
                if (!UnhookWindowsHookExNative(hook))
                {
                    if (!_hookReleaseFailureLogged)
                    {
                        Logger.LogWarning(
                            $"[TrayWheel] UnhookWindowsHookEx failed with error {Marshal.GetLastPInvokeError()}");
                        _hookReleaseFailureLogged = true;
                    }
                }
                else
                {
                    _hookReleaseFailureLogged = false;
                }
            }

            if (notify && generation != 0)
            {
                _disarmedHandler(generation);
            }
        }

        private void DrainSamples()
        {
            Interlocked.Exchange(ref _drainSamplesPosted, 0);
            if (_samples.Count == 0)
            {
                return;
            }

            var batch = _samples.ToArray();
            _samples.Clear();
            _sampleBatchHandler(batch);
        }

        private unsafe nint HookCallback(int nCode, nuint wParam, nint lParam)
        {
            if (nCode == HcAction && _armed)
            {
                var data = *(MsllHookStruct*)lParam;
                var message = (uint)wParam;

                if (message == WmMouseMove && !_activeBounds.Contains(data.Point.X, data.Point.Y))
                {
                    _armed = false;
                    _ = PostThreadMessageNative(_threadId, WmDisarm, 0, 0);
                }
                else if (message == WmMouseWheel)
                {
                    var delta = unchecked((short)(data.MouseData >> 16));
                    if (delta != 0)
                    {
                        if (_samples.Count == MaxQueuedSamples)
                        {
                            _ = _samples.Dequeue();
                        }

                        _samples.Enqueue(new TrayWheelSample(
                            data.Point.X,
                            data.Point.Y,
                            data.Time,
                            delta,
                            _activeGeneration));

                        if (Interlocked.Exchange(ref _drainSamplesPosted, 1) == 0 &&
                            !PostThreadMessageNative(_threadId, WmDrainSamples, 0, 0))
                        {
                            Interlocked.Exchange(ref _drainSamplesPosted, 0);
                        }

                        if (_activeBounds.Contains(data.Point.X, data.Point.Y))
                        {
                            // Consume the notch. Arming already required the UI thread to confirm
                            // that it will turn this into a brightness change, so forwarding it as
                            // well would scroll the focused window at the same time. Out-of-bounds
                            // samples are still queued so the UI thread can retire the hover, but
                            // they are not ours to swallow.
                            return HookHandled;
                        }
                    }
                }
            }

            return CallNextHookExNative(_hookHandle, nCode, wParam, lParam);
        }

        private void PostCommand(uint message, nuint wParam)
        {
            if (_threadStopped)
            {
                // The message loop is gone, so there is nothing to post to. Stay quiet: this is
                // reached from the tray hover path, once per mouse-move message.
                return;
            }

            if (!PostThreadMessageNative(_threadId, message, wParam, 0) && !_postFailureLogged)
            {
                _postFailureLogged = true;
                Logger.LogWarning(
                    $"[TrayWheel] PostThreadMessage failed with error {Marshal.GetLastPInvokeError()}");
            }
        }

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate nint LowLevelMouseProc(int nCode, nuint wParam, nint lParam);

        [StructLayout(LayoutKind.Sequential)]
        private struct NativePoint
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MsllHookStruct
        {
            public NativePoint Point;
            public uint MouseData;
            public uint Flags;
            public uint Time;
            public nuint ExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeMessage
        {
            public nint HWnd;
            public uint Message;
            public nuint WParam;
            public nint LParam;
            public uint Time;
            public NativePoint Point;
            public uint Private;
        }

        [LibraryImport("user32.dll", EntryPoint = "SetWindowsHookExW", SetLastError = true)]
        private static partial nint SetWindowsHookExNative(
            int hookType,
            nint hookProc,
            nint module,
            uint threadId);

        [LibraryImport("user32.dll", EntryPoint = "UnhookWindowsHookEx", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool UnhookWindowsHookExNative(nint hook);

        [LibraryImport("user32.dll", EntryPoint = "CallNextHookEx")]
        private static partial nint CallNextHookExNative(
            nint hook,
            int code,
            nuint wParam,
            nint lParam);

        [LibraryImport("user32.dll", EntryPoint = "GetMessageW", SetLastError = true)]
        private static partial int GetMessageNative(
            out NativeMessage message,
            nint window,
            uint minimumMessage,
            uint maximumMessage);

        [LibraryImport("user32.dll", EntryPoint = "PeekMessageW")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool PeekMessageNative(
            out NativeMessage message,
            nint window,
            uint minimumMessage,
            uint maximumMessage,
            uint removeMessage);

        [LibraryImport("user32.dll", EntryPoint = "PostThreadMessageW", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool PostThreadMessageNative(
            uint threadId,
            uint message,
            nuint wParam,
            nint lParam);

        [LibraryImport("kernel32.dll", EntryPoint = "GetCurrentThreadId")]
        private static partial uint GetCurrentThreadIdNative();

        [LibraryImport("kernel32.dll", EntryPoint = "GetModuleHandleW", StringMarshalling = StringMarshalling.Utf16)]
        private static partial nint GetModuleHandleNative(string? moduleName);
    }

    internal readonly record struct TrayWheelSample(
        int X,
        int Y,
        uint Timestamp,
        int Delta,
        long HoverGeneration);
}
