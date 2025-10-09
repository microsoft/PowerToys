// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using TopToolbar.Actions;
using TopToolbar.Logging;
using TopToolbar.Models;
using TopToolbar.Services.Profiles;
using TopToolbar.Services.Workspaces;

namespace TopToolbar.Providers
{
    public sealed class WorkspaceProvider : IActionProvider, IToolbarGroupProvider, IDisposable, IChangeNotifyingActionProvider
    {
        private const string WorkspacePrefix = "workspace.launch:";
        private readonly string _workspacesPath;
        private readonly WorkspacesRuntimeService _workspacesService;
        private readonly ProfileFileService _profileFileService;

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

        public WorkspaceProvider(string workspacesPath = null, ProfileFileService profileFileService = null)
        {
            _workspacesPath = string.IsNullOrWhiteSpace(workspacesPath)
                ? WorkspaceStoragePaths.GetDefaultWorkspacesPath()
                : workspacesPath;
            _workspacesService = new WorkspacesRuntimeService(_workspacesPath);
            _profileFileService = profileFileService ?? new ProfileFileService();
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
            List<WorkspaceRecord> oldList;
            lock (_cacheLock)
            {
                oldList = new List<WorkspaceRecord>(_cached);
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
                    // Synchronize workspace changes to all profiles
                    SyncWorkspacesToAllProfiles(oldList, newList);

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

        internal async Task<WorkspaceDefinition> SnapshotAsync(string workspaceName, CancellationToken cancellationToken)
        {
            ObjectDisposedException.ThrowIf(_disposed, nameof(WorkspaceProvider));

            var workspace = await _workspacesService.SnapshotAsync(workspaceName, cancellationToken).ConfigureAwait(false);
            if (workspace != null)
            {
                try
                {
                    await ReloadIfChangedAsync().ConfigureAwait(false);
                }
                catch
                {
                }
            }

            return workspace;
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
            try
            {
                var success = await _workspacesService.LaunchWorkspaceAsync(workspaceId, cancellationToken).ConfigureAwait(false);
                return success ? 0 : 1;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                AppLogger.LogWarning($"WorkspaceProvider: failed to launch workspace '{workspaceId}' - {ex.Message}");
                return 1;
            }
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

        /// <summary>
        /// Synchronizes workspace changes to all profiles.
        /// New workspaces are added as enabled by default.
        /// Deleted workspaces are removed from all profiles.
        /// </summary>
        private void SyncWorkspacesToAllProfiles(IReadOnlyList<WorkspaceRecord> oldWorkspaces, IReadOnlyList<WorkspaceRecord> newWorkspaces)
        {
            try
            {
                if (_profileFileService == null)
                {
                    return;
                }

                // Get all profiles
                var profiles = _profileFileService.GetAllProfiles();
                if (profiles == null || profiles.Count == 0)
                {
                    return;
                }

                // Find added workspaces (new ones that weren't in old list)
                var addedWorkspaces = newWorkspaces
                    .Where(nw => !oldWorkspaces.Any(ow => string.Equals(ow.Id, nw.Id, StringComparison.Ordinal)))
                    .ToList();

                // Find removed workspaces (old ones that aren't in new list)
                var removedWorkspaces = oldWorkspaces
                    .Where(ow => !newWorkspaces.Any(nw => string.Equals(nw.Id, ow.Id, StringComparison.Ordinal)))
                    .ToList();

                // Update each profile
                foreach (var profile in profiles)
                {
                    var modified = false;

                    // Find or create the workspaces group
                    var workspacesGroup = profile.Groups?.FirstOrDefault(g =>
                        string.Equals(g.Id, "workspaces", StringComparison.OrdinalIgnoreCase));

                    if (workspacesGroup == null)
                    {
                        // Create new workspaces group if it doesn't exist
                        workspacesGroup = new ProfileGroup
                        {
                            Id = "workspaces",
                            Name = "Workspaces",
                            Description = "Saved workspace layouts",
                            IsEnabled = true,
                            SortOrder = 0,
                            Actions = new List<ProfileAction>(),
                        };
                        profile.Groups ??= new List<ProfileGroup>();
                        profile.Groups.Add(workspacesGroup);
                        modified = true;
                    }

                    // Add new workspaces as enabled actions
                    foreach (var addedWorkspace in addedWorkspaces)
                    {
                        var actionId = $"workspace::{addedWorkspace.Id}";
                        var existingAction = workspacesGroup.Actions?.FirstOrDefault(a =>
                            string.Equals(a.Id, actionId, StringComparison.Ordinal));

                        if (existingAction == null)
                        {
                            var displayName = string.IsNullOrWhiteSpace(addedWorkspace.Name) ? addedWorkspace.Id : addedWorkspace.Name;
                            var newAction = new ProfileAction
                            {
                                Id = actionId,
                                Name = displayName,
                                Description = addedWorkspace.Id,
                                IsEnabled = true, // Enable new workspaces by default
                                IconGlyph = "\uE7F1",
                            };

                            workspacesGroup.Actions ??= new List<ProfileAction>();
                            workspacesGroup.Actions.Add(newAction);
                            modified = true;
                        }
                    }

                    // Remove deleted workspaces
                    if (workspacesGroup.Actions != null)
                    {
                        foreach (var removedWorkspace in removedWorkspaces)
                        {
                            var actionId = $"workspace::{removedWorkspace.Id}";
                            var actionToRemove = workspacesGroup.Actions.FirstOrDefault(a =>
                                string.Equals(a.Id, actionId, StringComparison.Ordinal));

                            if (actionToRemove != null)
                            {
                                workspacesGroup.Actions.Remove(actionToRemove);
                                modified = true;
                            }
                        }
                    }

                    // Save profile if modified
                    if (modified)
                    {
                        _profileFileService.SaveProfile(profile);
                    }
                }
            }
            catch (Exception ex)
            {
                // Log error but don't fail the workspace reload
                System.Diagnostics.Debug.WriteLine($"WorkspaceProvider: Failed to sync workspaces to profiles: {ex.Message}");
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

                try
                {
                    _workspacesService?.Dispose();
                }
                catch
                {
                }

                try
                {
                    _profileFileService?.Dispose();
                }
                catch
                {
                }

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

        /// <summary>
        /// Gets default workspace groups that should be added to profiles if they don't exist.
        /// This provides the standard workspace and MCP server groups with default enabled actions.
        /// </summary>
        public static async Task<List<TopToolbar.Models.ProfileGroup>> GetDefaultWorkspaceGroupsAsync()
        {
            var groups = new List<TopToolbar.Models.ProfileGroup>();

            // Create workspace provider instance to get real workspace data
            using var workspaceProvider = new WorkspaceProvider();

            try
            {
                // Get actual workspace group with real workspace data
                var context = new Actions.ActionContext();
                var workspaceButtonGroup = await workspaceProvider.CreateGroupAsync(context, CancellationToken.None);

                // Convert ButtonGroup to ProfileGroup
                var workspacesGroup = new TopToolbar.Models.ProfileGroup
                {
                    Id = workspaceButtonGroup.Id,
                    Name = workspaceButtonGroup.Name,
                    Description = workspaceButtonGroup.Description,
                    IsEnabled = true,
                    SortOrder = 0,
                    Actions = new List<TopToolbar.Models.ProfileAction>(),
                };

                // Convert each ToolbarButton to ProfileAction
                foreach (var button in workspaceButtonGroup.Buttons)
                {
                    var profileAction = new TopToolbar.Models.ProfileAction
                    {
                        Id = button.Id,
                        Name = button.Name,
                        Description = button.Description,
                        IsEnabled = true,
                        IconGlyph = button.IconGlyph,
                    };
                    workspacesGroup.Actions.Add(profileAction);
                }

                groups.Add(workspacesGroup);
            }
            catch (Exception)
            {
                // If workspace provider fails, add a minimal fallback group
                var fallbackGroup = new TopToolbar.Models.ProfileGroup
                {
                    Id = "workspaces",
                    Name = "Workspaces",
                    Description = "Saved workspace layouts",
                    IsEnabled = true,
                    SortOrder = 0,
                    Actions = new List<TopToolbar.Models.ProfileAction>(),
                };
                groups.Add(fallbackGroup);
            }

            return groups;
        }

        private sealed record WorkspaceRecord(string Id, string Name);
    }
}
