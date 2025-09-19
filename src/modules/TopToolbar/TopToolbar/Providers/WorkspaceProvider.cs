// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ManagedCommon;
using TopToolbar.Actions;
using TopToolbar.Models;

namespace TopToolbar.Providers
{
    public sealed class WorkspaceProvider : IActionProvider, IToolbarGroupProvider, IDisposable, IChangeNotifyingActionProvider
    {
        private const string WorkspacePrefix = "workspace.launch:";
        private readonly string _workspacesPath;

        // Caching + watcher fields
        private readonly object _cacheLock = new();
        private List<WorkspaceRecord> _cached = new();
        private bool _cacheLoaded;
        private int _version;
        private FileSystemWatcher _watcher;
        private System.Timers.Timer _debounceTimer;
        private bool _disposed;

        // Local event (UI or tests can hook) - optional
        public event EventHandler WorkspacesChanged;

        // Typed provider change event consumed by runtime
        public event EventHandler<ProviderChangedEventArgs> ProviderChanged;

        public WorkspaceProvider(string workspacesPath = null)
        {
            _workspacesPath = string.IsNullOrWhiteSpace(workspacesPath)
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "PowerToys", "Workspaces", "workspaces.json")
                : workspacesPath;
            StartWatcher();
        }

        private void StartWatcher()
        {
            try
            {
                var dir = Path.GetDirectoryName(_workspacesPath);
                var file = Path.GetFileName(_workspacesPath);
                if (string.IsNullOrWhiteSpace(dir) || string.IsNullOrWhiteSpace(file))
                {
                    return;
                }

                _debounceTimer = new System.Timers.Timer(250) { AutoReset = false };
                _debounceTimer.Elapsed += async (_, __) =>
                {
                    try
                    {
                        if (await ReloadIfChangedAsync().ConfigureAwait(false))
                        {
                            WorkspacesChanged?.Invoke(this, EventArgs.Empty);
                        }
                    }
                    catch (Exception)
                    {
                        // Swallow, optional: add logging later
                    }
                };

                _watcher = new FileSystemWatcher(dir, file)
                {
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName | NotifyFilters.CreationTime,
                    IncludeSubdirectories = false,
                    EnableRaisingEvents = true,
                };

                FileSystemEventHandler handler = (_, __) => RestartDebounce();
                RenamedEventHandler renamedHandler = (_, __) => RestartDebounce();
                _watcher.Changed += handler;
                _watcher.Created += handler;
                _watcher.Deleted += handler;
                _watcher.Renamed += renamedHandler;
            }
            catch (Exception)
            {
                // Ignore watcher setup failures
            }
        }

        private void RestartDebounce()
        {
            if (_debounceTimer == null)
            {
                return;
            }

            try
            {
                _debounceTimer.Stop();
                _debounceTimer.Start();
            }
            catch
            {
            }
        }

        private async Task<bool> ReloadIfChangedAsync()
        {
            var newList = await ReadWorkspacesFileAsync(CancellationToken.None).ConfigureAwait(false);
            bool changed;
            lock (_cacheLock)
            {
                if (!HasChanged(_cached, newList))
                {
                    return false;
                }

                _cached = new List<WorkspaceRecord>(newList);
                _cacheLoaded = true;
                _version++;
                changed = true;
            }

            if (changed)
            {
                try
                {
                    WorkspacesChanged?.Invoke(this, EventArgs.Empty);

                    // Use ActionsUpdated with the set of current workspace action ids
                    var actionIds = new List<string>();
                    foreach (var ws in newList)
                    {
                        actionIds.Add("workspace::" + ws.Id);
                    }

                    ProviderChanged?.Invoke(this, ProviderChangedEventArgs.ActionsUpdated(Id, actionIds));
                }
                catch
                {
                }
            }

            return true;
        }

        private static bool HasChanged(List<WorkspaceRecord> oldList, IReadOnlyList<WorkspaceRecord> newList)
        {
            if (oldList.Count != newList.Count
            )
            {
                return true;
            }

            for (int i = 0; i < oldList.Count; i++)
            {
                var o = oldList[i];

                var n = newList[i];

                if (!string.Equals(o.Id, n.Id, StringComparison.Ordinal) || !string.Equals(o.Name, n.Name, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private async Task<IReadOnlyList<WorkspaceRecord>> GetWorkspacesAsync(CancellationToken cancellationToken)
        {
            if (_cacheLoaded)
            {
                lock (_cacheLock)
                {
                    return _cached;
                }
            }

            var list = await ReadWorkspacesFileAsync(cancellationToken).ConfigureAwait(false);
            lock (_cacheLock)
            {
                if (!_cacheLoaded)
                {
                    _cached = new List<WorkspaceRecord>(list);
                    _cacheLoaded = true;
                    _version = 1;
                }

                return _cached;
            }
        }

        public string Id => "WorkspaceProvider";

        public Task<ProviderInfo> GetInfoAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(new ProviderInfo("Workspaces", "1.0"));
        }

        public async IAsyncEnumerable<ActionDescriptor> DiscoverAsync(ActionContext context, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            var workspaces = await GetWorkspacesAsync(cancellationToken).ConfigureAwait(false);
            var order = 0d;
            foreach (var workspace in workspaces)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var displayName = string.IsNullOrWhiteSpace(workspace.Name) ? workspace.Id : workspace.Name;
                var descriptor = new ActionDescriptor
                {
                    Id = WorkspacePrefix + workspace.Id,
                    ProviderId = Id,
                    Title = displayName,
                    Subtitle = workspace.Id,
                    Kind = ActionKind.Launch,
                    GroupHint = "workspaces",
                    Order = order++,
                    Icon = new ActionIcon { Type = ActionIconType.Glyph, Value = "\uE7F1" },
                    CanExecute = true,
                };

                if (!string.IsNullOrWhiteSpace(workspace.Name))
                {
                    descriptor.Keywords.Add(workspace.Name);
                }

                descriptor.Keywords.Add(workspace.Id);
                yield return descriptor;
            }
        }

        public async Task<ButtonGroup> CreateGroupAsync(ActionContext context, CancellationToken cancellationToken)
        {
            var group = new ButtonGroup
            {
                Id = "workspaces",
                Name = "Workspaces",
                Description = "Saved workspace layouts",
                Layout = new ToolbarGroupLayout
                {
                    Style = ToolbarGroupLayoutStyle.Capsule,
                    Overflow = ToolbarGroupOverflowMode.Menu,
                    MaxInline = 8,
                },
            };

            var workspaces = await GetWorkspacesAsync(cancellationToken).ConfigureAwait(false);
            foreach (var workspace in workspaces)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var displayName = string.IsNullOrWhiteSpace(workspace.Name) ? workspace.Id : workspace.Name;
                var button = new ToolbarButton
                {
                    Id = $"workspace::{workspace.Id}",
                    Name = displayName,
                    Description = workspace.Id,
                    IconGlyph = "\uE7F1",
                    Action = new ToolbarAction
                    {
                        Type = ToolbarActionType.Provider,
                        ProviderId = Id,
                        ProviderActionId = WorkspacePrefix + workspace.Id,
                    },
                };

                group.Buttons.Add(button);
            }

            return group;
        }

        public async Task<ActionResult> InvokeAsync(
            string actionId,
            JsonElement? args,
            ActionContext context,
            IProgress<ActionProgress> progress,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(actionId) || !actionId.StartsWith(WorkspacePrefix, StringComparison.OrdinalIgnoreCase))
            {
                return new ActionResult
                {
                    Ok = false,
                    Message = "Invalid workspace action id.",
                };
            }

            var workspaceId = actionId.Substring(WorkspacePrefix.Length).Trim();
            if (string.IsNullOrWhiteSpace(workspaceId))
            {
                return new ActionResult
                {
                    Ok = false,
                    Message = "Workspace identifier is empty.",
                };
            }

            try
            {
                var exitCode = await RunLauncherAsync(workspaceId, cancellationToken).ConfigureAwait(false);
                var ok = exitCode == 0;
                return new ActionResult
                {
                    Ok = ok,
                    Message = ok ? string.Empty : $"Launcher exit code {exitCode}.",
                };
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                // TODO: Add proper logging once Logger reference is resolved
                return new ActionResult
                {
                    Ok = false,
                    Message = ex.Message,
                };
            }
        }

        private async Task<int> RunLauncherAsync(string workspaceId, CancellationToken cancellationToken)
        {
            if (!TryResolveLauncher(out var launcherPath))
            {
                throw new FileNotFoundException("PowerToys.WorkspacesLauncher.exe not found.");
            }

            var arguments = $"{workspaceId} {(int)InvokePoint.Shortcut}";
            var startInfo = new ProcessStartInfo(launcherPath)
            {
                Arguments = arguments,
                UseShellExecute = false,
                WorkingDirectory = Path.GetDirectoryName(launcherPath) ?? AppContext.BaseDirectory,
            };

            using var process = Process.Start(startInfo);
            if (process == null)
            {
                throw new InvalidOperationException("Failed to start WorkspacesLauncher.");
            }

            using (cancellationToken.Register(() =>
            {
                try
                {
                    if (!process.HasExited)
                    {
                        process.Kill();
                    }
                }
                catch
                {
                }
            }))
            {
                await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
                return process.ExitCode;
            }
        }

        private bool TryResolveLauncher(out string path)
        {
            // First try in the current application directory
            path = Path.Combine(AppContext.BaseDirectory, "PowerToys.WorkspacesLauncher.exe");
            if (File.Exists(path))
            {
                return true;
            }

            // TopToolbar app runs in WinUI3Apps folder, so look one level up to find the launcher
            var parentDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, ".."));
            var fallback = Path.Combine(parentDir, "PowerToys.WorkspacesLauncher.exe");
            if (File.Exists(fallback))
            {
                path = fallback;
                return true;
            }

            return false;
        }

        private async Task<IReadOnlyList<WorkspaceRecord>> ReadWorkspacesFileAsync(CancellationToken cancellationToken)
        {
            if (!File.Exists(_workspacesPath))
            {
                return Array.Empty<WorkspaceRecord>();
            }

            try
            {
                await using var stream = new FileStream(_workspacesPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
                if (!document.RootElement.TryGetProperty("workspaces", out var workspacesElement) || workspacesElement.ValueKind != JsonValueKind.Array)
                {
                    return Array.Empty<WorkspaceRecord>();
                }

                var list = new List<WorkspaceRecord>();
                foreach (var item in workspacesElement.EnumerateArray())
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (!item.TryGetProperty("id", out var idElement))
                    {
                        continue;
                    }

                    var id = idElement.GetString();
                    if (string.IsNullOrWhiteSpace(id))
                    {
                        continue;
                    }

                    string name = null;
                    if (item.TryGetProperty("name", out var nameElement))
                    {
                        name = nameElement.GetString();
                    }

                    list.Add(new WorkspaceRecord(id.Trim(), name?.Trim()));
                }

                return list;
            }
            catch (JsonException)
            {
                // TODO: Add proper logging once Logger reference is resolved
                // Logger.LogWarning($"WorkspaceProvider: failed to parse '{_workspacesPath} - {ex.Message}'.");
            }
            catch (IOException)
            {
                // TODO: Add proper logging once Logger reference is resolved
                // Logger.LogWarning($"WorkspaceProvider: unable to read '{_workspacesPath} - {ex.Message}'.");
            }

            return Array.Empty<WorkspaceRecord>();
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
                try
                {
                    _debounceTimer?.Stop();
                }
                catch
                {
                }

                try
                {
                    _debounceTimer?.Dispose();
                }
                catch
                {
                }

                _debounceTimer = null;

                try
                {
                    if (_watcher != null)
                    {
                        _watcher.EnableRaisingEvents = false;
                        _watcher.Dispose();
                    }
                }
                catch
                {
                }

                _watcher = null;

                lock (_cacheLock)
                {
                    _cached.Clear();
                    _cacheLoaded = false;
                    _version = 0;
                }

                // Release any external subscribers
                WorkspacesChanged = null;
                ProviderChanged = null;
            }
            finally
            {
                GC.SuppressFinalize(this);
            }
        }

        private sealed record WorkspaceRecord(string Id, string Name);

        private enum InvokePoint
        {
            EditorButton = 0,
            Shortcut = 1,
            LaunchAndEdit = 2,
        }
    }
}
