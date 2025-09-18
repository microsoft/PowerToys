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
    public sealed class WorkspaceProvider : IActionProvider, IToolbarGroupProvider
    {
        private const string WorkspacePrefix = "workspace.launch:";
        private readonly string _workspacesPath;

        public WorkspaceProvider(string workspacesPath = null)
        {
            _workspacesPath = string.IsNullOrWhiteSpace(workspacesPath)
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "PowerToys", "Workspaces", "workspaces.json")
                : workspacesPath;
        }

        public string Id => "WorkspaceProvider";

        public Task<ProviderInfo> GetInfoAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(new ProviderInfo("Workspaces", "1.0"));
        }

        public async IAsyncEnumerable<ActionDescriptor> DiscoverAsync(ActionContext context, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            var workspaces = await LoadWorkspacesAsync(cancellationToken).ConfigureAwait(false);
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

            var workspaces = await LoadWorkspacesAsync(cancellationToken).ConfigureAwait(false);
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
                Logger.LogError($"WorkspaceProvider.InvokeAsync: failed to launch workspace - {ex.Message}");
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
            path = Path.Combine(AppContext.BaseDirectory, "PowerToys.WorkspacesLauncher.exe");
            if (File.Exists(path))
            {
                return true;
            }

            var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", ".."));
            var fallback = Path.Combine(repoRoot, "PowerToys.WorkspacesLauncher.exe");
            if (File.Exists(fallback))
            {
                path = fallback;
                return true;
            }

            return false;
        }

        private async Task<IReadOnlyList<WorkspaceRecord>> LoadWorkspacesAsync(CancellationToken cancellationToken)
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
            catch (JsonException ex)
            {
                Logger.LogWarning($"WorkspaceProvider: failed to parse '{_workspacesPath} - {ex.Message}'.");
            }
            catch (IOException ex)
            {
                Logger.LogWarning($"WorkspaceProvider: unable to read '{_workspacesPath} - {ex.Message}'.");
            }

            return Array.Empty<WorkspaceRecord>();
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
