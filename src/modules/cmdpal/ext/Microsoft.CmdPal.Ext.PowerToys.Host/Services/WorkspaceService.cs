// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Threading;
using PowerToys.ModuleContracts;
using PowerToys.Interop;
using static Common.UI.SettingsDeepLink;

namespace Microsoft.CmdPal.Ext.PowerToys.Services;

/// <summary>
/// Implementation of workspace actions that can be reused by thin command adapters.
/// </summary>
internal sealed class WorkspaceService : IWorkspaceService
{
    public string Key => SettingsWindow.Workspaces.ToString();

    public Task<OperationResult> LaunchAsync(CancellationToken cancellationToken = default)
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

    public Task<OperationResult> OpenSettingsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var entry = new Classes.PowerToysModuleEntry
            {
                Module = SettingsWindow.Workspaces,
            };

            entry.NavigateToSettingsPage();
            return Task.FromResult(OperationResult.Ok());
        }
        catch (Exception ex)
        {
            return Task.FromResult(OperationResult.Fail($"Failed to open Workspaces settings: {ex.Message}"));
        }
    }

    public Task<OperationResult> SnapshotAsync(string? targetPath = null, CancellationToken cancellationToken = default)
    {
        // Snapshot orchestration is not yet exposed via events; provide a clear failure for now.
        return Task.FromResult(OperationResult.Fail("Snapshot is not implemented for Workspaces."));
    }
}
