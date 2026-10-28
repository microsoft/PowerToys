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
        if (!Guid.TryParse(workspaceId, out var id) || id == Guid.Empty)
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
                Arguments = id.ToString("B"),
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

    public async Task<OperationResult<IReadOnlyList<ProjectWrapper>>> GetWorkspacesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var items = await WorkspacesStorage.LoadAsync(cancellationToken).ConfigureAwait(false);

            return OperationResults.Ok<IReadOnlyList<ProjectWrapper>>(items);
        }
        catch (Exception ex)
        {
            return OperationResults.Fail<IReadOnlyList<ProjectWrapper>>($"Failed to read workspaces: {ex.Message} Open Workspaces Editor to set up protected storage or retry.");
        }
    }
}
