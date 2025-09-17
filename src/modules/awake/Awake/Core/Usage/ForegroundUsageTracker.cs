// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Timers;
using Awake.Core.Native;
using Awake.Core.Usage.Models;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library;

namespace Awake.Core.Usage
{
    internal sealed class ForegroundUsageTracker : IDisposable
    {
        private const uint EventSystemForeground = 0x0003;
        private const uint WinEventOutOfContext = 0x0000;
        private const double CommitThresholdSeconds = 0.25;

        private static readonly JsonSerializerOptions LegacySerializer = new()
        {
            WriteIndented = true,
        };

        private delegate void WinEventDelegate(
            IntPtr hWinEventHook,
            uint eventType,
            IntPtr hwnd,
            int idObject,
            int idChild,
            uint dwEventThread,
            uint dwmsEventTime);

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

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        private readonly object _lock = new();
        private readonly string _legacyJsonPath;
        private readonly string _dbPath;
        private readonly Timer _flushTimer;
        private readonly Timer _pollTimer;
        private readonly TimeSpan _idleThreshold = TimeSpan.FromSeconds(60);
        private readonly Dictionary<string, AppUsageRecord> _sessionCache = new(StringComparer.OrdinalIgnoreCase);

        private IUsageStore _store;

        private string? _activeProcess;
        private DateTime _activeSince;
        private IntPtr _hook;
        private WinEventDelegate? _hookDelegate;
        private IntPtr _lastHwnd;
        private int _retentionDays;
        private bool _disposed;

        internal bool Enabled { get; private set; }

        public ForegroundUsageTracker(string legacyJsonPath, int retentionDays)
        {
            _legacyJsonPath = legacyJsonPath;
            _dbPath = Path.Combine(Path.GetDirectoryName(legacyJsonPath)!, "usage.sqlite");
            _retentionDays = retentionDays;
            _store = new SqliteUsageStore(_dbPath);

            _flushTimer = new Timer(5000)
            {
                AutoReset = true,
            };
            _flushTimer.Elapsed += (_, _) => FlushInternal();

            _pollTimer = new Timer(1000)
            {
                AutoReset = true,
            };
            _pollTimer.Elapsed += (_, _) => PollForeground();

            TryImportLegacy();
        }

        private void TryImportLegacy()
        {
            try
            {
                if (!File.Exists(_legacyJsonPath))
                {
                    return;
                }

                string json = File.ReadAllText(_legacyJsonPath);
                List<AppUsageRecord> list = JsonSerializer.Deserialize<List<AppUsageRecord>>(json, LegacySerializer) ?? new();
                foreach (AppUsageRecord r in list)
                {
                    _store.AddSpan(r.ProcessName, r.TotalSeconds, r.FirstSeenUtc, r.LastUpdatedUtc, _retentionDays);
                }

                Logger.LogInfo("[AwakeUsage] Imported legacy usage.json into SQLite. Deleting old file.");
                File.Delete(_legacyJsonPath);
            }
            catch (Exception ex)
            {
                Logger.LogWarning("[AwakeUsage] Legacy import failed: " + ex.Message);
            }
        }

        public void Configure(bool enabled, int retentionDays)
        {
            _retentionDays = Math.Max(1, retentionDays);
            if (enabled == Enabled)
            {
                return;
            }

            Enabled = enabled;
            if (Enabled)
            {
                _activeSince = DateTime.UtcNow;
                _hookDelegate = WinEventCallback;
                _hook = SetWinEventHook(EventSystemForeground, EventSystemForeground, IntPtr.Zero, _hookDelegate, 0, 0, WinEventOutOfContext);
                Logger.LogInfo(_hook != IntPtr.Zero ? "[AwakeUsage] WinEvent hook installed." : "[AwakeUsage] WinEvent hook failed (poll fallback)");
                CaptureInitialForeground();
                _flushTimer.Start();
                _pollTimer.Start();
                Logger.LogInfo("[AwakeUsage] Tracking enabled (5s flush, sqlite store).");
            }
            else
            {
                _flushTimer.Stop();
                _pollTimer.Stop();
                if (_hook != IntPtr.Zero)
                {
                    UnhookWinEvent(_hook);
                    _hook = IntPtr.Zero;
                }

                CommitActiveSpan();
                FlushInternal(force: true);
                Logger.LogInfo("[AwakeUsage] Tracking disabled.");
            }
        }

        private void WinEventCallback(
            IntPtr hWinEventHook,
            uint evt,
            IntPtr hwnd,
            int idObj,
            int idChild,
            uint thread,
            uint time)
        {
            if (_disposed || !Enabled || evt != EventSystemForeground)
            {
                return;
            }

            HandleForegroundChange(hwnd, "hook");
        }

        private void PollForeground()
        {
            if (_disposed || !Enabled)
            {
                return;
            }

            IntPtr hwnd = GetForegroundWindow();
            if (hwnd == IntPtr.Zero || hwnd == _lastHwnd)
            {
                return;
            }

            HandleForegroundChange(hwnd, "poll");
        }

        private void CaptureInitialForeground()
        {
            IntPtr hwnd = GetForegroundWindow();
            if (hwnd == IntPtr.Zero)
            {
                return;
            }

            if (TryResolveProcess(hwnd, out string? name))
            {
                _activeProcess = name;
                _activeSince = DateTime.UtcNow;
                _lastHwnd = hwnd;
            }
        }

        private bool TryResolveProcess(IntPtr hwnd, out string? name)
        {
            name = null;
            try
            {
                uint pid;
                uint tid = GetWindowThreadProcessId(hwnd, out pid);
                if (tid == 0 || pid == 0)
                {
                    return false;
                }

                using Process p = Process.GetProcessById((int)pid);
                name = SafeProcessName(p);
                return !string.IsNullOrWhiteSpace(name);
            }
            catch
            {
                return false;
            }
        }

        private static string SafeProcessName(Process p)
        {
            try
            {
                return Path.GetFileName(p.MainModule?.FileName) ?? p.ProcessName;
            }
            catch
            {
                return p.ProcessName;
            }
        }

        private void HandleForegroundChange(IntPtr hwnd, string source)
        {
            try
            {
                CommitActiveSpan();
                if (!TryResolveProcess(hwnd, out string? name))
                {
                    _activeProcess = null;
                    return;
                }

                _activeProcess = name;
                _activeSince = DateTime.UtcNow;
                _lastHwnd = hwnd;
            }
            catch (Exception ex)
            {
                Logger.LogWarning("[AwakeUsage] FG change failed: " + ex.Message);
            }
        }

        private void CommitActiveSpan()
        {
            if (string.IsNullOrEmpty(_activeProcess))
            {
                return;
            }

            if (IdleTime.GetIdleTime() > _idleThreshold)
            {
                _activeProcess = null;
                return;
            }

            double secs = (DateTime.UtcNow - _activeSince).TotalSeconds;
            if (secs < CommitThresholdSeconds)
            {
                return;
            }

            lock (_lock)
            {
                if (!_sessionCache.TryGetValue(_activeProcess!, out AppUsageRecord? rec))
                {
                    rec = new AppUsageRecord
                    {
                        ProcessName = _activeProcess!,
                        FirstSeenUtc = DateTime.UtcNow,
                        LastUpdatedUtc = DateTime.UtcNow,
                        TotalSeconds = 0,
                    };
                    _sessionCache[_activeProcess!] = rec;
                }

                rec.TotalSeconds += secs;
                rec.LastUpdatedUtc = DateTime.UtcNow;
            }

            _activeSince = DateTime.UtcNow;
        }

        private void FlushInternal(bool force = false)
        {
            try
            {
                CommitActiveSpan();

                Dictionary<string, AppUsageRecord> snapshot;
                lock (_lock)
                {
                    snapshot = _sessionCache.ToDictionary(k => k.Key, v => v.Value);
                    _sessionCache.Clear();
                }

                foreach (AppUsageRecord rec in snapshot.Values)
                {
                    _store.AddSpan(rec.ProcessName, rec.TotalSeconds, rec.FirstSeenUtc, rec.LastUpdatedUtc, _retentionDays);
                }

                if (force)
                {
                    _store.Prune(_retentionDays);
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning("[AwakeUsage] Flush failed: " + ex.Message);
            }
        }

        public IReadOnlyList<AppUsageRecord> GetSummary(int top, int days)
        {
            CommitActiveSpan();
            FlushInternal();
            try
            {
                return _store.Query(top, days);
            }
            catch (Exception ex)
            {
                Logger.LogWarning("[AwakeUsage] Query failed: " + ex.Message);
                return Array.Empty<AppUsageRecord>();
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            Configure(false, _retentionDays);
            _store.Dispose();
            _flushTimer.Dispose();
            _pollTimer.Dispose();
        }
    }
}
