// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.IO;
using Common.UI;
using ManagedCommon;
using PowerToys.Interop;
using PowerToys.ModuleContracts;
using WorkspacesCsharpLibrary.Data;
using WorkspacesCsharpLibrary.SettingsService;

namespace Workspaces.ModuleServices;

/// <summary>
/// Implementation of workspace actions for reuse across hosts.
/// </summary>
public sealed class WorkspaceService : ModuleServiceBase, IWorkspaceService
{
    public static WorkspaceService Instance { get; } = new();

    public override string Key => SettingsDeepLink.SettingsWindow.Workspaces.ToString();

    protected override SettingsDeepLink.SettingsWindow SettingsWindow => SettingsDeepLink.SettingsWindow.Workspaces;

    public override Task<OperationResult> LaunchAsync(CancellationToken cancellationToken = default)
    {
        // Treat launch as invoking the Workspaces editor.
        return LaunchEditorAsync(cancellationToken);
    }

    public Task<OperationResult> LaunchEditorAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var eventHandle = new EventWaitHandle(false, EventResetMode.AutoReset, Constants.WorkspacesLaunchEditorEvent());
            eventHandle.Set();
            return Task.FromResult(OperationResult.Ok());
        }
        catch (Exception ex)
        {
            return Task.FromResult(OperationResult.Fail($"Failed to launch Workspaces editor: {ex.Message}"));
        }
    }

    public Task<OperationResult> LaunchWorkspaceAsync(string workspaceId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(workspaceId))
        {
            return Task.FromResult(OperationResult.Fail("Workspace id is required."));
        }

        try
        {
            var powertoysBaseDir = PowerToysPathResolver.GetPowerToysInstallPath();
            if (string.IsNullOrEmpty(powertoysBaseDir))
            {
                return Task.FromResult(OperationResult.Fail("PowerToys installation path not found."));
            }

            var launcherPath = Path.Combine(powertoysBaseDir, "PowerToys.WorkspacesLauncher.exe");
            var startInfo = new ProcessStartInfo(launcherPath)
            {
                Arguments = workspaceId,
                UseShellExecute = true,
            };

            Process.Start(startInfo);
            return Task.FromResult(OperationResult.Ok());
        }
        catch (Exception ex)
        {
            return Task.FromResult(OperationResult.Fail($"Failed to launch workspace: {ex.Message}"));
        }
    }

    public Task<OperationResult> SnapshotAsync(string? targetPath = null, CancellationToken cancellationToken = default)
    {
        // Snapshot orchestration is not yet exposed via events; provide a clear failure for now.
        return Task.FromResult(OperationResult.Fail("Snapshot is not implemented for Workspaces."));
    }

    public Task<OperationResult<IReadOnlyList<ProjectWrapper>>> GetWorkspacesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            EnsureMigrationBackstop();

            var items = WorkspacesStorage.Load();

            return Task.FromResult(OperationResults.Ok<IReadOnlyList<ProjectWrapper>>(items));
        }
        catch (Exception ex)
        {
            return Task.FromResult(OperationResults.Fail<IReadOnlyList<ProjectWrapper>>($"Failed to read workspaces: {ex.Message}"));
        }
    }

    // One-shot legacy-migration backstop (Design-v6-Final.md §10/§11).  Primary
    // seeding happens at install (per-machine) or the lazy hardening step
    // (per-user); this catches stragglers — a user whose legacy file appeared
    // after install.  Idempotent (sentinel-guarded inside Run); runs at most
    // once per process and never blocks reads.
    private static int _migrationChecked;

    private static void EnsureMigrationBackstop()
    {
        if (Interlocked.Exchange(ref _migrationChecked, 1) != 0)
        {
            return;
        }

        try
        {
            WorkspacesMigration.Run();
        }
        catch (Exception)
        {
            // Best-effort backstop; on failure reads fall back per WorkspacesStorage.
        }
    }
}
