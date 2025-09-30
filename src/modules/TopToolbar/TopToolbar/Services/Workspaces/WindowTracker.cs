// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace TopToolbar.Services.Workspaces
{
    internal sealed class WindowTracker : IDisposable
    {
        private const uint EventObjectCreate = 0x8000;
        private const uint EventObjectDestroy = 0x8001;
        private const uint EventObjectShow = 0x8002;
        private const uint EventObjectHide = 0x8003;
        private const uint EventObjectNameChange = 0x800C;
        private const uint EventSystemForeground = 0x0003;
        private const uint EventFlagOutOfContext = 0x0000;
        private const uint EventFlagSkipOwnProcess = 0x0002;
        private const int ObjectIdWindow = 0;

        private readonly Dictionary<IntPtr, WindowInfo> _windows = new();
        private readonly List<IntPtr> _hookHandles = new();
        private readonly object _gate = new();
        private readonly WinEventDelegate _winEventCallback;
        private bool _disposed;

        public WindowTracker()
        {
            _winEventCallback = OnWinEvent;
            RefreshAllWindows();
            StartListening();
        }

        public IReadOnlyList<WindowInfo> GetSnapshot()
        {
            lock (_gate)
            {
                return new List<WindowInfo>(_windows.Values);
            }
        }

        public IReadOnlyList<WindowInfo> FindMatches(ApplicationDefinition app)
        {
            return FindMatches(app, 0);
        }

        public IReadOnlyList<WindowInfo> FindMatches(ApplicationDefinition app, uint expectedProcessId)
        {
            if (app == null)
            {
                return Array.Empty<WindowInfo>();
            }

            lock (_gate)
            {
                if (_windows.Count == 0)
                {
                    return Array.Empty<WindowInfo>();
                }

                var matches = new List<WindowInfo>();
                foreach (var window in _windows.Values)
                {
                    if (expectedProcessId != 0 && window.ProcessId != expectedProcessId)
                    {
                        continue;
                    }

                    if (NativeWindowHelper.IsMatch(window, app))
                    {
                        matches.Add(window);
                    }
                }

                return matches;
            }
        }

        public async Task<IReadOnlyList<WindowInfo>> WaitForAppWindowsAsync(
            ApplicationDefinition app,
            IReadOnlyCollection<IntPtr> knownHandles,
            uint expectedProcessId,
            TimeSpan timeout,
            TimeSpan pollInterval,
            CancellationToken cancellationToken)
        {
            var known = knownHandles != null && knownHandles.Count > 0
                ? new HashSet<IntPtr>(knownHandles)
                : new HashSet<IntPtr>();

            var deadline = Stopwatch.StartNew();
            while (deadline.Elapsed < timeout)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var matches = FindMatches(app, expectedProcessId);
                if (matches.Count > 0)
                {
                    var newMatches = new List<WindowInfo>();
                    foreach (var match in matches)
                    {
                        if (!known.Contains(match.Handle))
                        {
                            newMatches.Add(match);
                        }
                    }

                    if (newMatches.Count > 0)
                    {
                        return newMatches;
                    }
                }

                await Task.Delay(pollInterval, cancellationToken).ConfigureAwait(false);
            }

            return Array.Empty<WindowInfo>();
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            lock (_gate)
            {
                foreach (var handle in _hookHandles)
                {
                    if (handle != IntPtr.Zero)
                    {
                        _ = UnhookWinEvent(handle);
                    }
                }

                _hookHandles.Clear();
                _windows.Clear();
                _disposed = true;
            }

            GC.SuppressFinalize(this);
        }

        private void StartListening()
        {
            RegisterHook(EventSystemForeground);
            RegisterHook(EventObjectCreate);
            RegisterHook(EventObjectDestroy);
            RegisterHook(EventObjectShow);
            RegisterHook(EventObjectHide);
            RegisterHook(EventObjectNameChange);
        }

        private void RegisterHook(uint eventType)
        {
            var handle = SetWinEventHook(eventType, eventType, IntPtr.Zero, _winEventCallback, 0, 0, EventFlagOutOfContext | EventFlagSkipOwnProcess);
            if (handle != IntPtr.Zero)
            {
                _hookHandles.Add(handle);
            }
        }

        private void RefreshAllWindows()
        {
            lock (_gate)
            {
                _windows.Clear();
            }

            _ = EnumWindows(
                (hwnd, _) =>
                {
                    RefreshWindow(hwnd);
                    return true;
                },
                IntPtr.Zero);
        }

        private void RefreshWindow(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero)
            {
                return;
            }

            if (!NativeWindowHelper.TryCreateWindowInfo(hwnd, out var info))
            {
                RemoveWindow(hwnd);
                return;
            }

            lock (_gate)
            {
                _windows[hwnd] = info;
            }
        }

        private void RemoveWindow(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero)
            {
                return;
            }

            lock (_gate)
            {
                _ = _windows.Remove(hwnd);
            }
        }

        private void OnWinEvent(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
        {
            if (idObject != ObjectIdWindow || idChild != 0 || hwnd == IntPtr.Zero)
            {
                return;
            }

            switch (eventType)
            {
                case EventObjectDestroy:
                    RemoveWindow(hwnd);
                    break;
                case EventObjectHide:
                case EventObjectShow:
                case EventObjectCreate:
                case EventObjectNameChange:
                case EventSystemForeground:
                    RefreshWindow(hwnd);
                    break;
                default:
                    break;
            }
        }

        private delegate void WinEventDelegate(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern IntPtr SetWinEventHook(
            uint eventMin,
            uint eventMax,
            IntPtr hmodWinEventProc,
            WinEventDelegate lpfnWinEventProc,
            uint idProcess,
            uint idThread,
            uint dwFlags);

        [DllImport("user32.dll")]
        private static extern bool UnhookWinEvent(IntPtr hWinEventHook);

        private delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);
    }
}
