// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;

#nullable enable

namespace WorkspacesCsharpLibrary.SettingsService;

/// <summary>
/// Orchestrates the two settings blocks — service initialization
/// (<see cref="ServiceProvisioner"/>) and settings-file migration
/// (<see cref="WorkspacesMigration"/>) — behind a single entry point that can be
/// invoked from any number of trigger points (editor open, first save, workspace
/// launch, an explicit Settings toggle).  Keeping the orchestration here means
/// new trigger points only have to call <see cref="EnsureInitialized"/>; they
/// don't need to know the ordering or guards.
/// </summary>
public static class SettingsBootstrapper
{
    /// <summary>Where the bootstrap was invoked from (diagnostics / policy).</summary>
    public enum TriggerReason
    {
        /// <summary>The Workspaces editor was opened / its list loaded.</summary>
        EditorOpened,

        /// <summary>A workspace is about to be saved.</summary>
        WorkspaceSaving,

        /// <summary>A workspace is about to be launched.</summary>
        WorkspaceLaunching,

        /// <summary>The user explicitly asked to enable protection.</summary>
        ExplicitUserRequest,
    }

    /// <summary>Combined result of a bootstrap pass.</summary>
    public readonly record struct Result(
        ServiceProvisioner.Outcome Provision,
        WorkspacesMigration.Outcome Migration);

    // Auto (non-forced) bootstrap runs at most once per process to keep the hot
    // path (every editor open) cheap; an explicit user request always runs.
    private static int _autoBootstrapped;

    /// <summary>
    /// Ensures the service is provisioned (if a payload is available) and that
    /// this user's legacy data has been migrated.  Safe to call repeatedly and
    /// from multiple trigger points.
    /// </summary>
    /// <param name="request">Trigger, install folder and provisioning knobs.</param>
    public static Result EnsureInitialized(BootstrapRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Explicit one-off actions (an explicit "enable protection" request, or a
        // save) always run and always re-attempt.  Automatic triggers (editor
        // open) run their provisioning at most once per process, so a single
        // editor session prompts at most once.
        var alwaysRun = request.Reason is TriggerReason.ExplicitUserRequest
                                       or TriggerReason.WorkspaceSaving;

        if (!alwaysRun && Interlocked.Exchange(ref _autoBootstrapped, 1) != 0)
        {
            // Already ran the automatic pass this process; nothing cheap left to do.
            return new Result(ServiceProvisioner.Outcome.AlreadyAttempted, WorkspacesMigration.Outcome.AlreadyMigrated);
        }

        // Whether to bypass the version-scoped ATTEMPT SENTINEL.  Any trigger that
        // reflects direct user intent to use Workspaces (opening the editor,
        // saving, or an explicit enable) forces a fresh provisioning attempt.  The
        // sentinel persists across uninstall (it lives under %LocalAppData%), so a
        // same-version REINSTALL — or reopening the editor after a declined/failed
        // prompt — must still re-prompt instead of being permanently stuck.  Only
        // passive/background paths honor the sentinel back-off.
        var force = request.Reason is TriggerReason.ExplicitUserRequest
                                   or TriggerReason.WorkspaceSaving
                                   or TriggerReason.EditorOpened;

        // Block 1: service initialization.  Only attempt when we have an install
        // folder to locate the payload; otherwise skip straight to migration,
        // which has its own no-service fallback.
        var provision = ServiceProvisioner.Outcome.PayloadMissing;
        if (!string.IsNullOrEmpty(request.InstallFolder))
        {
            try
            {
                var options = ProvisionOptions.FromInstallFolder(request.InstallFolder!, force);
                if (request.ElevationRunner != null)
                {
                    options = new ProvisionOptions
                    {
                        ServiceBinaryPath = options.ServiceBinaryPath,
                        ServiceMsixPath = options.ServiceMsixPath,
                        UserSid = options.UserSid,
                        Force = force,
                        ElevationRunner = request.ElevationRunner,
                    };
                }

                provision = ServiceProvisioner.EnsureProvisioned(options);
            }
            catch (Exception)
            {
                // Provisioning is best-effort; fall through to migration so the
                // editor still works via the no-service fallback.
                provision = ServiceProvisioner.Outcome.ElevationFailed;
            }
        }

        // Block 2: settings-file migration.  Idempotent; when the service is up
        // this seeds the protected blob, otherwise it no-ops cleanly.
        var migration = WorkspacesMigration.Outcome.SkippedServiceUnavailable;
        try
        {
            migration = WorkspacesMigration.Run();
        }
        catch (Exception)
        {
            // Best-effort backstop; reads fall back per WorkspacesStorage.
        }

        return new Result(provision, migration);
    }
}
