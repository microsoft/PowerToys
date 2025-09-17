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
    /// <summary>
    /// Tracks foreground application usage time (simple active window focus durations) with idle suppression.
    /// </summary>
    internal sealed class ForegroundUsageTracker : IDisposable
    {
        private const uint EventSystemForeground = 0x0003;
        private const uint WinEventOutOfContext = 0x0000;

        private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };

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
        private readonly Dictionary<string, AppUsageRecord> _usage = new(StringComparer.OrdinalIgnoreCase);
        private readonly string _storePath;
        private readonly Timer _flushTimer;
        private readonly TimeSpan _idleThreshold = TimeSpan.FromSeconds(60);
        private readonly Timer _pollTimer; // Fallback polling when WinEvent hook not firing

        private string? _activeProcess;
        private DateTime _activeSince;
        private bool _disposed;
        private IntPtr _hook = IntPtr.Zero;
        private WinEventDelegate? _hookDelegate;
        private int _retentionDays;
        private IntPtr _lastHwnd = IntPtr.Zero;

        private const double CommitThresholdSeconds = 0.25;

        internal bool Enabled { get; private set; }

        public ForegroundUsageTracker(string storePath, int retentionDays)
        {
            _storePath = storePath;
            _retentionDays = retentionDays;
            Directory.CreateDirectory(Path.GetDirectoryName(storePath)!);

            LoadState();

            _flushTimer = new Timer(30000)
            {
                AutoReset = true,
            };
            _flushTimer.Elapsed += (_, _) => FlushInternal();

            _pollTimer = new Timer(1000)
            {
                AutoReset = true,
            };
            _pollTimer.Elapsed += (_, _) => PollForeground();
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
                Logger.LogInfo(_hook != IntPtr.Zero ? "[AwakeUsage] WinEvent hook installed." : "[AwakeUsage] WinEvent hook failed (fallback polling only).");
                CaptureInitialForeground();
                _flushTimer.Start();
                _pollTimer.Start();
                Logger.LogInfo("[AwakeUsage] Started foreground usage tracking.");
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
                FlushInternal();
                Logger.LogInfo("[AwakeUsage] Stopped foreground usage tracking.");
            }
        }

        private void CaptureInitialForeground()
        {
            try
            {
                IntPtr hwnd = GetForegroundWindow();
                if (hwnd == IntPtr.Zero)
                {
                    Logger.LogDebug("[AwakeUsage] Initial foreground hwnd == 0.");
                    return;
                }

                if (TryResolveProcess(hwnd, out string? procName))
                {
                    _activeProcess = procName;
                    _activeSince = DateTime.UtcNow;
                    _lastHwnd = hwnd;
                    EnsurePlaceholder(_activeProcess);
                    Logger.LogInfo("[AwakeUsage] Initial foreground captured: " + _activeProcess);
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning("[AwakeUsage] Failed to capture initial foreground: " + ex.Message);
            }
        }

        private static string SafeResolveProcessName(Process proc)
        {
            try
            {
                return Path.GetFileName(proc.MainModule?.FileName) ?? proc.ProcessName;
            }
            catch
            {
                return proc.ProcessName;
            }
        }

        private bool TryResolveProcess(IntPtr hwnd, out string? processName)
        {
            processName = null;

            if (hwnd == IntPtr.Zero)
            {
                return false;
            }

            try
            {
                uint pid;
                uint tid = GetWindowThreadProcessId(hwnd, out pid);
                if (tid == 0 || pid == 0)
                {
                    return false;
                }

                using Process p = Process.GetProcessById((int)pid);
                processName = SafeResolveProcessName(p);
                return !string.IsNullOrWhiteSpace(processName);
            }
            catch (Exception ex)
            {
                Logger.LogDebug("[AwakeUsage] TryResolveProcess failed: " + ex.Message);
                return false;
            }
        }

        private void EnsurePlaceholder(string? process)
        {
            if (string.IsNullOrWhiteSpace(process))
            {
                return;
            }

            lock (_lock)
            {
                if (!_usage.ContainsKey(process))
                {
                    _usage[process] = new AppUsageRecord
                    {
                        ProcessName = process,
                        FirstSeenUtc = DateTime.UtcNow,
                        LastUpdatedUtc = DateTime.UtcNow,
                        TotalSeconds = 0,
                    };
                }
            }
        }

        private void WinEventCallback(IntPtr hWinEventHook, uint evt, IntPtr hwnd, int idObj, int idChild, uint thread, uint time)
        {
            if (_disposed || !Enabled || evt != EventSystemForeground)
            {
                return;
            }

            Logger.LogDebug($"[AwakeUsage] WinEvent foreground change: hwnd=0x{hwnd.ToInt64():X}");
            HandleForegroundChange(hwnd, source: "hook");
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

            Logger.LogDebug($"[AwakeUsage] Poll detected change: hwnd=0x{hwnd.ToInt64():X}");
            HandleForegroundChange(hwnd, source: "poll");
        }

        private void HandleForegroundChange(IntPtr hwnd, string source)
        {
            try
            {
                CommitActiveSpan();
                if (!TryResolveProcess(hwnd, out string? procName))
                {
                    _activeProcess = null;
                    return;
                }

                _activeProcess = procName;
                _activeSince = DateTime.UtcNow;
                _lastHwnd = hwnd;
                EnsurePlaceholder(_activeProcess);
                Logger.LogDebug($"[AwakeUsage] Active process set ({source}): {_activeProcess}");
            }
            catch (Exception ex)
            {
                Logger.LogWarning("[AwakeUsage] HandleForegroundChange failed: " + ex.Message);
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

            double seconds = (DateTime.UtcNow - _activeSince).TotalSeconds;
            if (seconds < CommitThresholdSeconds)
            {
                return;
            }

            lock (_lock)
            {
                if (!_usage.TryGetValue(_activeProcess!, out AppUsageRecord? rec))
                {
                    rec = new AppUsageRecord
                    {
                        ProcessName = _activeProcess!,
                        FirstSeenUtc = DateTime.UtcNow,
                        LastUpdatedUtc = DateTime.UtcNow,
                        TotalSeconds = 0,
                    };
                    _usage[_activeProcess!] = rec;
                }

                rec.TotalSeconds += seconds;
                rec.LastUpdatedUtc = DateTime.UtcNow;
            }

            _activeSince = DateTime.UtcNow;
        }

        private void LoadState()
        {
            try
            {
                if (!File.Exists(_storePath))
                {
                    return;
                }

                string json = File.ReadAllText(_storePath);
                List<AppUsageRecord>? list = JsonSerializer.Deserialize<List<AppUsageRecord>>(json);
                if (list == null)
                {
                    return;
                }

                DateTime cutoff = DateTime.UtcNow.AddDays(-_retentionDays);
                foreach (AppUsageRecord rec in list.Where(r => r.LastUpdatedUtc >= cutoff))
                {
                    _usage[rec.ProcessName] = rec;
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning("[AwakeUsage] Failed to load usage store: " + ex.Message);
            }
        }

        private double GetLiveActiveSeconds()
        {
            if (string.IsNullOrEmpty(_activeProcess))
            {
                return 0;
            }

            if (IdleTime.GetIdleTime() > _idleThreshold)
            {
                return 0;
            }

            return Math.Max(0, (DateTime.UtcNow - _activeSince).TotalSeconds);
        }

        private void FlushInternal()
        {
            try
            {
                CommitActiveSpan();

                List<AppUsageRecord> snapshot;
                double liveSeconds = GetLiveActiveSeconds();
                string? liveProcess = _activeProcess;
                lock (_lock)
                {
                    DateTime cutoff = DateTime.UtcNow.AddDays(-_retentionDays);
                    foreach (string key in _usage.Values.Where(v => v.LastUpdatedUtc < cutoff).Select(v => v.ProcessName).ToList())
                    {
                        _usage.Remove(key);
                    }

                    snapshot = _usage.Values
                        .Select(r => new AppUsageRecord
                        {
                            ProcessName = r.ProcessName,
                            TotalSeconds = r.ProcessName.Equals(liveProcess, StringComparison.OrdinalIgnoreCase) ? r.TotalSeconds + liveSeconds : r.TotalSeconds,
                            FirstSeenUtc = r.FirstSeenUtc,
                            LastUpdatedUtc = r.LastUpdatedUtc,
                        })
                        .OrderByDescending(r => r.TotalSeconds)
                        .ToList();

                    if (liveProcess != null && !_usage.ContainsKey(liveProcess) && liveSeconds > 0)
                    {
                        snapshot.Add(new AppUsageRecord
                        {
                            ProcessName = liveProcess,
                            TotalSeconds = liveSeconds,
                            FirstSeenUtc = DateTime.UtcNow,
                            LastUpdatedUtc = DateTime.UtcNow,
                        });
                        snapshot = snapshot.OrderByDescending(r => r.TotalSeconds).ToList();
                    }
                }

                string json = JsonSerializer.Serialize(snapshot, SerializerOptions);
                File.WriteAllText(_storePath, json);
            }
            catch (Exception ex)
            {
                Logger.LogWarning("[AwakeUsage] Flush failed: " + ex.Message);
            }
        }

        public IReadOnlyList<AppUsageRecord> GetSummary(int top, int days)
        {
            CommitActiveSpan();
            double liveSeconds = GetLiveActiveSeconds();
            string? liveProcess = _activeProcess;

            lock (_lock)
            {
                DateTime cutoff = DateTime.UtcNow.AddDays(-Math.Max(1, days));
                List<AppUsageRecord> list = _usage.Values
                    .Where(r => r.LastUpdatedUtc >= cutoff)
                    .Select(r => new AppUsageRecord
                    {
                        ProcessName = r.ProcessName,
                        TotalSeconds = r.ProcessName.Equals(liveProcess, StringComparison.OrdinalIgnoreCase) ? r.TotalSeconds + liveSeconds : r.TotalSeconds,
                        FirstSeenUtc = r.FirstSeenUtc,
                        LastUpdatedUtc = r.LastUpdatedUtc,
                    })
                    .ToList();

                if (liveProcess != null && list.All(r => !r.ProcessName.Equals(liveProcess, StringComparison.OrdinalIgnoreCase)) && liveSeconds > 0)
                {
                    list.Add(new AppUsageRecord
                    {
                        ProcessName = liveProcess,
                        TotalSeconds = liveSeconds,
                        FirstSeenUtc = DateTime.UtcNow,
                        LastUpdatedUtc = DateTime.UtcNow,
                    });
                }

                return list
                    .OrderByDescending(r => r.TotalSeconds)
                    .Take(top)
                    .ToList();
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
            _flushTimer.Dispose();
            _pollTimer.Dispose();
        }
    }
}
