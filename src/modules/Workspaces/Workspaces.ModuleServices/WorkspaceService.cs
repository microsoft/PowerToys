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
            EnsureSettingsInitialized(SettingsBootstrapper.TriggerReason.WorkspaceLaunching);

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
            // Enumeration is a PASSIVE read (e.g. the Command Palette listing
            // workspaces at startup).  It must NOT trigger provisioning: doing so
            // popped a UAC right after install, before the user had even engaged
            // with Workspaces.  Provisioning is reserved for EXPLICIT actions
            // (opening the editor, saving, launching a workspace).  If the service
            // isn't up yet, Load returns empty (protected-only, never plaintext).
            var items = WorkspacesStorage.Load();

            return Task.FromResult(OperationResults.Ok<IReadOnlyList<ProjectWrapper>>(items));
        }
        catch (Exception ex)
        {
            return Task.FromResult(OperationResults.Fail<IReadOnlyList<ProjectWrapper>>($"Failed to read workspaces: {ex.Message}"));
        }
    }

    // Deferred settings initialization (Design-v6-Final.md §11).  Composes the
    // service-initialization and legacy-migration blocks behind one call so new
    // trigger points only have to invoke SettingsBootstrapper.EnsureInitialized.
    // On a per-machine install the service is already up, so provisioning is a
    // no-op and only the migration backstop runs.  On a per-user install with no
    // service yet, this performs the one-time elevation to register + harden it.
    private static void EnsureSettingsInitialized(
        SettingsBootstrapper.TriggerReason reason = SettingsBootstrapper.TriggerReason.EditorOpened)
    {
        try
        {
            SettingsBootstrapper.EnsureInitialized(new BootstrapRequest
            {
                Reason = reason,
                InstallFolder = PowerToysPathResolver.GetPowerToysInstallPath(),
            });
        }
        catch (Exception)
        {
            // Best-effort; on failure reads fall back per WorkspacesStorage.
        }
    }
}
