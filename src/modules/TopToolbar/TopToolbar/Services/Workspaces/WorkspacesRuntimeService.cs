// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using TopToolbar.Logging;

namespace TopToolbar.Services.Workspaces
{
    internal sealed partial class WorkspacesRuntimeService : IDisposable
    {
        private static readonly TimeSpan WindowWaitTimeout = TimeSpan.FromSeconds(20);
        private static readonly TimeSpan WindowPollInterval = TimeSpan.FromMilliseconds(200);

        private readonly WorkspaceFileLoader _fileLoader;
        private readonly WindowTracker _windowTracker;
        private bool _disposed;

        private readonly record struct AppWindowResult(bool Succeeded, bool LaunchedNewInstance, IReadOnlyList<WindowInfo> Windows)
        {
            public static AppWindowResult Failed => new(false, false, Array.Empty<WindowInfo>());
        }

        public WorkspacesRuntimeService(string workspacesPath = null)
        {
            _fileLoader = new WorkspaceFileLoader(workspacesPath);
            _windowTracker = new WindowTracker();
        }


        public async Task<WorkspaceDefinition> SnapshotAsync(string workspaceName, CancellationToken cancellationToken)
        {
            ObjectDisposedException.ThrowIf(_disposed, nameof(WorkspacesRuntimeService));

            if (string.IsNullOrWhiteSpace(workspaceName))
            {
                throw new ArgumentException("Workspace name cannot be null or empty.", nameof(workspaceName));
            }

            var trimmedName = workspaceName.Trim();
            var monitorSnapshots = CaptureMonitorSnapshots();
            var windows = _windowTracker.GetSnapshot();
            var applications = new List<ApplicationDefinition>();

            foreach (var window in windows)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (window == null || !window.IsVisible || window.ProcessId == (uint)Environment.ProcessId)
                {
                    continue;
                }

                var app = CreateApplicationDefinitionFromWindow(window, monitorSnapshots);
                if (app != null)
                {
                    applications.Add(app);
                }
            }

            if (applications.Count == 0)
            {
                return null;
            }

            var workspace = new WorkspaceDefinition
            {
                Id = Guid.NewGuid().ToString("N"),
                Name = trimmedName,
                CreationTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                IsShortcutNeeded = false,
                MoveExistingWindows = true,
                Applications = applications,
            };

            if (monitorSnapshots.Count > 0)
            {
                var monitorDefinitions = new List<MonitorDefinition>(monitorSnapshots.Count);
                foreach (var snapshot in monitorSnapshots)
                {
                    monitorDefinitions.Add(snapshot.Definition);
                }

                workspace.Monitors = monitorDefinitions;
            }
            else
            {
                workspace.Monitors = new List<MonitorDefinition>();
            }

            await _fileLoader.SaveWorkspaceAsync(workspace, cancellationToken).ConfigureAwait(false);
            return workspace;
        }


        public async Task<bool> LaunchWorkspaceAsync(string workspaceId, CancellationToken cancellationToken)
        {
            ObjectDisposedException.ThrowIf(_disposed, nameof(WorkspacesRuntimeService));

            if (string.IsNullOrWhiteSpace(workspaceId))
            {
                throw new ArgumentException("Workspace ID cannot be null or empty", nameof(workspaceId));
            }

            var workspace = await _fileLoader.LoadByIdAsync(workspaceId, cancellationToken).ConfigureAwait(false);
            if (workspace == null)
            {
                AppLogger.LogWarning($"WorkspaceRuntime: workspace '{workspaceId}' not found.");
                return false;
            }

            var context = new WorkspaceExecutionContext(_windowTracker, workspace);
            var anySuccess = false;

            foreach (var app in workspace.Applications)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    var result = await MakeSureAppAliveAsync(app, context, cancellationToken).ConfigureAwait(false);
                    if (!result.Succeeded || result.Windows.Count == 0)
                    {
                        continue;
                    }

                    var window = result.Windows[0];
                    NativeWindowHelper.SetWindowPlacement(window.Handle, app.Position, app.Maximized, app.Minimized, result.LaunchedNewInstance);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    AppLogger.LogWarning($"WorkspaceRuntime: failed to launch '{app?.Id ?? "<null>"}' - {ex.Message}");
                }
            }

            MinimizeExtraneousWindows(context);
            return anySuccess;
        }

        private async Task<AppWindowResult> MakeSureAppAliveAsync(ApplicationDefinition app, WorkspaceExecutionContext context, CancellationToken cancellationToken)
        {
            if (app == null)
            {
                return AppWindowResult.Failed;
            }

            var existingWindows = context.GetWorkspaceWindows(app);
            if (existingWindows.Count > 0)
            {
                return new AppWindowResult(true, false, existingWindows);
            }

            return await LaunchApplicationAsync(app, context, cancellationToken).ConfigureAwait(false);
        }

        private async Task<AppWindowResult> LaunchApplicationAsync(ApplicationDefinition app, WorkspaceExecutionContext context, CancellationToken cancellationToken)
        {
            if (app == null)
            {
                return AppWindowResult.Failed;
            }

            if (!string.IsNullOrWhiteSpace(app.AppUserModelId))
            {
                return await LaunchPackagedAppAsync(app, context, cancellationToken).ConfigureAwait(false);
            }

            if (!string.IsNullOrWhiteSpace(app.Path))
            {
                return await LaunchWin32AppAsync(app, context, cancellationToken).ConfigureAwait(false);
            }

            return AppWindowResult.Failed;
        }

        private async Task<AppWindowResult> LaunchPackagedAppAsync(ApplicationDefinition app, WorkspaceExecutionContext context, CancellationToken cancellationToken)
        {
            var knownHandles = context.GetKnownHandles(app);

            try
            {
                var activationManager = (IApplicationActivationManager)new ApplicationActivationManager();
                var hr = activationManager.ActivateApplication(
                    app.AppUserModelId,
                    string.IsNullOrWhiteSpace(app.CommandLineArguments) ? string.Empty : app.CommandLineArguments,
                    ActivateOptions.None,
                    out var processId);

                if (hr < 0)
                {
                    Marshal.ThrowExceptionForHR(hr);
                }

                await WaitAndMergeWindowsAsync(app, context, knownHandles, processId, cancellationToken).ConfigureAwait(false);
                return new AppWindowResult(true, true, context.GetWorkspaceWindows(app));
            }
            catch (COMException ex) when ((uint)ex.HResult == 0x80040154)
            {
                AppLogger.LogWarning($"WorkspaceRuntime: ApplicationActivationManager not registered. Falling back to shell launch for '{app.AppUserModelId}'.");
                return await LaunchPackagedAppViaShellAsync(app, context, knownHandles, cancellationToken).ConfigureAwait(false);
            }
            catch (COMException ex)
            {
                AppLogger.LogWarning($"WorkspaceRuntime: ActivateApplication failed for '{app.AppUserModelId}' - 0x{ex.HResult:X8} {ex.Message}. Falling back to shell launch.");
                return await LaunchPackagedAppViaShellAsync(app, context, knownHandles, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                AppLogger.LogWarning($"WorkspaceRuntime: Unexpected error launching '{app.AppUserModelId}' - {ex.Message}. Falling back to shell launch.");
                return await LaunchPackagedAppViaShellAsync(app, context, knownHandles, cancellationToken).ConfigureAwait(false);
            }
        }

        private async Task<AppWindowResult> LaunchPackagedAppViaShellAsync(
            ApplicationDefinition app,
            WorkspaceExecutionContext context,
            IReadOnlyCollection<IntPtr> knownHandles,
            CancellationToken cancellationToken)
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"shell:appsFolder\\{app.AppUserModelId}",
                    UseShellExecute = true,
                };

                using var process = Process.Start(startInfo);
                if (process == null)
                {
                    return AppWindowResult.Failed;
                }

                await WaitAndMergeWindowsAsync(app, context, knownHandles, 0, cancellationToken).ConfigureAwait(false);
                return new AppWindowResult(true, true, context.GetWorkspaceWindows(app));
            }
            catch (Exception ex)
            {
                AppLogger.LogWarning($"WorkspaceRuntime: shell launch failed for '{app.AppUserModelId}' - {ex.Message}");
                return AppWindowResult.Failed;
            }
        }

        private async Task<AppWindowResult> LaunchWin32AppAsync(ApplicationDefinition app, WorkspaceExecutionContext context, CancellationToken cancellationToken)
        {
            var knownHandles = context.GetKnownHandles(app);
            var expandedPath = ExpandPath(app.Path);
            var useShellExecute = expandedPath.StartsWith("shell:", StringComparison.OrdinalIgnoreCase) || !File.Exists(expandedPath);

            var startInfo = new ProcessStartInfo
            {
                FileName = expandedPath,
                Arguments = string.IsNullOrWhiteSpace(app.CommandLineArguments) ? string.Empty : app.CommandLineArguments,
                UseShellExecute = useShellExecute,
                WorkingDirectory = DetermineWorkingDirectory(expandedPath, useShellExecute),
            };

            if (app.IsElevated && app.CanLaunchElevated)
            {
                startInfo.Verb = "runas";
                startInfo.UseShellExecute = true;
            }

            try
            {
                using var process = Process.Start(startInfo);
                if (process == null)
                {
                    AppLogger.LogWarning($"WorkspaceRuntime: process start returned null for '{expandedPath}'.");
                    return AppWindowResult.Failed;
                }

                var targetProcessId = (uint)process.Id;
                if (process.HasExited)
                {
                    var succeeded = process.ExitCode == 0;
                    await WaitAndMergeWindowsAsync(app, context, knownHandles, targetProcessId, cancellationToken).ConfigureAwait(false);
                    return succeeded ? new AppWindowResult(true, true, context.GetWorkspaceWindows(app)) : AppWindowResult.Failed;
                }

                await WaitAndMergeWindowsAsync(app, context, knownHandles, targetProcessId, cancellationToken).ConfigureAwait(false);
                return new AppWindowResult(true, true, context.GetWorkspaceWindows(app));
            }
            catch (Win32Exception ex)
            {
                AppLogger.LogWarning($"WorkspaceRuntime: Win32Exception launching '{expandedPath}' - {ex.Message} ({ex.NativeErrorCode}).");
                return AppWindowResult.Failed;
            }
            catch (Exception ex)
            {
                AppLogger.LogWarning($"WorkspaceRuntime: failed to start '{expandedPath}' - {ex.Message}");
                return AppWindowResult.Failed;
            }
        }

        private async Task WaitAndMergeWindowsAsync(
            ApplicationDefinition app,
            WorkspaceExecutionContext context,
            IReadOnlyCollection<IntPtr> knownHandles,
            uint expectedProcessId,
            CancellationToken cancellationToken)
        {
            var windows = await _windowTracker.WaitForAppWindowsAsync(
                app,
                knownHandles,
                expectedProcessId,
                WindowWaitTimeout,
                WindowPollInterval,
                cancellationToken).ConfigureAwait(false);

            IReadOnlyList<WindowInfo> matches = windows;
            if (matches.Count == 0)
            {
                matches = expectedProcessId != 0
                    ? _windowTracker.FindMatches(app, expectedProcessId)
                    : _windowTracker.FindMatches(app);
            }

            if (matches.Count > 0)
            {
                context.MergeWindows(app, matches, markLaunched: true);
            }
        }

        private void MinimizeExtraneousWindows(WorkspaceExecutionContext context)
        {
            try
            {
                var currentProcessId = (uint)Environment.ProcessId;
                var keepHandles = context.GetWorkspaceHandles();
                var snapshot = _windowTracker.GetSnapshot();

                foreach (var window in snapshot)
                {
                    if (window.ProcessId == currentProcessId)
                    {
                        continue;
                    }

                    if (keepHandles.Contains(window.Handle))
                    {
                        continue;
                    }

                    if (!NativeWindowHelper.CanMinimizeWindow(window.Handle))
                    {
                        continue;
                    }

                    NativeWindowHelper.MinimizeWindow(window.Handle);
                }
            }
            catch (Exception ex)
            {
                AppLogger.LogWarning($"WorkspaceRuntime: failed to minimize extraneous windows - {ex.Message}");
            }
        }

        private static string ExpandPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }

            try
            {
                return Environment.ExpandEnvironmentVariables(path).Trim('"');
            }
            catch
            {
                return path.Trim('"');
            }
        }

        private static string DetermineWorkingDirectory(string path, bool useShellExecute)
        {
            if (useShellExecute)
            {
                return AppContext.BaseDirectory;
            }

            try
            {
                var directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    return directory;
                }
            }
            catch
            {
            }

            return AppContext.BaseDirectory;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _windowTracker.Dispose();
            GC.SuppressFinalize(this);
        }

        [ComImport]
        [Guid("45BA127D-10A8-46EA-8AB7-56EA9078943C")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IApplicationActivationManager
        {
            int ActivateApplication(string appUserModelId, string arguments, ActivateOptions options, out uint processId);

            int ActivateForFile(string appUserModelId, IntPtr itemArray, string verb, out uint processId);

            int ActivateForProtocol(string appUserModelId, IntPtr itemArray, out uint processId);
        }

        [ComImport]
        [Guid("2E941141-7F97-4756-BA1D-9DECDE894A3D")]
        private class ApplicationActivationManager
        {
        }

        [Flags]
        private enum ActivateOptions
        {
            None = 0x0,
            DesignMode = 0x1,
            NoErrorUI = 0x2,
            NoSplashScreen = 0x4,
        }

        [DllImport("user32.dll")]
        private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumProc lpfnEnum, IntPtr dwData);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFOEX lpmi);

        [DllImport("shcore.dll")]
        private static extern int GetDpiForMonitor(IntPtr hmonitor, MonitorDpiType dpiType, out uint dpiX, out uint dpiY);

        private sealed class MonitorSnapshot
        {
            public MonitorSnapshot(MonitorDefinition definition, MonitorBounds bounds)
            {
                Definition = definition;
                Bounds = bounds;
            }

            public MonitorDefinition Definition { get; }

            public MonitorBounds Bounds { get; }
        }

        private readonly struct MonitorBounds
        {
            public MonitorBounds(int left, int top, int right, int bottom)
            {
                Left = left;
                Top = top;
                Right = right;
                Bottom = bottom;
            }

            public int Left { get; }

            public int Top { get; }

            public int Right { get; }

            public int Bottom { get; }

            public int Width => Right - Left;

            public int Height => Bottom - Top;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeMonitorRect
        {
            public int Left;

            public int Top;

            public int Right;

            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct MONITORINFOEX
        {
            public int CbSize;

            public NativeMonitorRect RcMonitor;

            public NativeMonitorRect RcWork;

            public uint DwFlags;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string SzDevice;
        }

        private delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdcMonitor, ref NativeMonitorRect lprcMonitor, IntPtr dwData);

        private enum MonitorDpiType
        {
            EffectiveDpi = 0,
            AngularDpi = 1,
            RawDpi = 2,
        }


        private sealed class WorkspaceExecutionContext
        {
            private readonly Dictionary<ApplicationDefinition, ApplicationState> _applicationStates = new();
            private readonly HashSet<IntPtr> _workspaceHandles = new();

            public WorkspaceExecutionContext(WindowTracker tracker, WorkspaceDefinition workspace)
            {
                if (workspace?.Applications == null)
                {
                    return;
                }

                foreach (var app in workspace.Applications)
                {
                    var state = GetState(app);
                    var matches = tracker.FindMatches(app);
                    if (matches.Count > 0)
                    {
                        state.Merge(matches, markLaunched: false);
                        foreach (var window in matches)
                        {
                            if (window != null && window.Handle != IntPtr.Zero)
                            {
                                _workspaceHandles.Add(window.Handle);
                            }
                        }
                    }
                }
            }

            public IReadOnlyList<WindowInfo> GetWorkspaceWindows(ApplicationDefinition app)
            {
                return GetState(app).GetWindowsSnapshot();
            }

            public IReadOnlyCollection<IntPtr> GetKnownHandles(ApplicationDefinition app)
            {
                return GetState(app).GetHandleSnapshot();
            }

            public HashSet<IntPtr> GetWorkspaceHandles()
            {
                return new HashSet<IntPtr>(_workspaceHandles);
            }

            public void MergeWindows(ApplicationDefinition app, IReadOnlyList<WindowInfo> windows, bool markLaunched)
            {
                if (app == null || windows == null || windows.Count == 0)
                {
                    return;
                }

                var state = GetState(app);
                state.Merge(windows, markLaunched);
                foreach (var window in windows)
                {
                    if (window != null && window.Handle != IntPtr.Zero)
                    {
                        _workspaceHandles.Add(window.Handle);
                    }
                }
            }

            private ApplicationState GetState(ApplicationDefinition app)
            {
                if (app == null)
                {
                    return new ApplicationState();
                }

                if (!_applicationStates.TryGetValue(app, out var state))
                {
                    state = new ApplicationState();
                    _applicationStates[app] = state;
                }

                return state;
            }
        }

        private sealed class ApplicationState
        {
            private readonly Dictionary<IntPtr, WindowInfo> _windows = new();
            private bool _launched;

            public IReadOnlyList<WindowInfo> GetWindowsSnapshot()
            {
                return new List<WindowInfo>(_windows.Values);
            }

            public IReadOnlyCollection<IntPtr> GetHandleSnapshot()
            {
                return new List<IntPtr>(_windows.Keys);
            }

            public bool WasLaunched => _launched;

            public void Merge(IEnumerable<WindowInfo> windows, bool markLaunched)
            {
                if (windows != null)
                {
                    foreach (var window in windows)
                    {
                        if (window == null || window.Handle == IntPtr.Zero)
                        {
                            continue;
                        }

                        _windows[window.Handle] = window;
                    }
                }

                if (markLaunched)
                {
                    _launched = true;
                }
            }
        }
    }
}
