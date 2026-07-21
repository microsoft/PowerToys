// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ManagedCommon;
using WorkspacesCsharpLibrary.Data;
using WorkspacesCsharpLibrary.SettingsService;
using WorkspacesCsharpLibrary.Utils;
using WorkspacesEditor.Models;
using WorkspacesEditor.ViewModels;

namespace WorkspacesEditor.Utils
{
    public class WorkspacesEditorIO
    {
        public WorkspacesEditorIO()
        {
        }

        public ParsingResult ParseWorkspaces(MainViewModel mainViewModel, bool runBootstrap = true, bool showDialogs = true)
        {
            try
            {
                // Deferred per-user service init + legacy migration (Design §11 / §14.1).
                // On a per-machine install the service is already up (no-op); on a
                // per-user install with no service yet, this performs the one-time
                // elevation to register + harden it, then migrates the legacy file.
                //
                // This can take a while (UAC + MSIX deploy, which is serialized
                // machine-wide and may queue behind a PowerToys upgrade).  The editor
                // therefore does the FIRST load with runBootstrap:false so the window
                // shows immediately, then runs the bootstrap OFF the UI thread and
                // reloads (see App.OnStartup).
                if (runBootstrap)
                {
                    TryBootstrapSettings();
                }

                WorkspacesData parser = new();
                WorkspacesData.WorkspacesListWrapper workspaces;

                // v6: read the settings through the service (GetBlob).  Fall back to
                // the legacy %LocalAppData% file only when no service is installed
                // (no-admin / declined-UAC), per §10.
                var rc = PTSettingsClient.GetBlob(out var blob);
                switch (rc)
                {
                    case PTSettingsClient.Result.Ok:
                        workspaces = parser.Deserialize(Encoding.UTF8.GetString(blob));
                        break;

                    case PTSettingsClient.Result.NotFound:
                        // Service is up but this user has no blob yet (first run).
                        return new ParsingResult(true);

                    case PTSettingsClient.Result.Unavailable:
                        // Protected-store-only: do NOT read the stale, user-writable
                        // legacy file.  The protected %ProgramData% store is the single
                        // source of truth; once migration has seeded it, the legacy
                        // file is out of date.  On the fast initial load the background
                        // provisioning (App.OnStartup) provisions + migrates and then
                        // reloads from the protected store.  If it is still unavailable
                        // on a user-facing load, protection simply isn't set up yet —
                        // tell the user (their data is preserved and appears once
                        // protection is enabled) rather than showing stale plaintext.
                        Logger.LogWarning("GetBlob unavailable; protected store not reachable (no plaintext fallback).");
                        if (showDialogs)
                        {
                            System.Windows.MessageBox.Show(
                                "PowerToys couldn't load your workspaces because the protected Workspaces settings service isn't set up yet. " +
                                "Workspaces are stored only in a protected, tamper-resistant location — the older unprotected file is no longer used. " +
                                "Setting it up needs a one-time administrator approval: reopen the Workspaces editor (or restart PowerToys) and accept the prompt. " +
                                "Your existing workspaces are preserved and will appear once protection is enabled.",
                                "Workspaces",
                                System.Windows.MessageBoxButton.OK,
                                System.Windows.MessageBoxImage.Warning);
                        }

                        return new ParsingResult(true);

                    case PTSettingsClient.Result.AuthRejected:
                        // The protected settings EXIST but this app wasn't authorized
                        // by the service (typically a version mismatch in the transient
                        // window right after an update, before re-provisioning).  Do NOT
                        // silently show an empty list (looks like data loss) — reassure
                        // and point at the fix.  Data is untouched.  Suppressed on the
                        // fast initial load (showDialogs=false); shown on the post-
                        // provision reload if the service is still unreachable.
                        Logger.LogWarning("GetBlob rejected by the settings service (AuthRejected).");
                        if (showDialogs)
                        {
                            System.Windows.MessageBox.Show(
                                "PowerToys couldn't load your saved workspaces because the settings service didn't authorize this app. " +
                                "Your workspaces are safe — this usually happens right after a PowerToys update. " +
                                "Restart PowerToys (or reopen the Workspaces editor) to finish setup, then they'll reappear.",
                                "Workspaces",
                                System.Windows.MessageBoxButton.OK,
                                System.Windows.MessageBoxImage.Warning);
                        }

                        return new ParsingResult(true);

                    default:
                        // Protocol / IoError → fail safe to empty (transient/unknown).
                        Logger.LogWarning($"GetBlob returned {rc}; treating workspaces as empty.");
                        return new ParsingResult(true);
                }

                if (workspaces.Workspaces == null)
                {
                    return new ParsingResult(true);
                }

                if (!SetWorkspaces(mainViewModel, workspaces))
                {
                    Logger.LogWarning($"Workspaces storage file content could not be set. Reason: {Properties.Resources.Error_Parsing_Message}");
                    return new ParsingResult(false, WorkspacesEditor.Properties.Resources.Error_Parsing_Message);
                }

                return new ParsingResult(true);
            }
            catch (Exception e)
            {
                Logger.LogError($"Exception while parsing storage file: {e.Message}");
                return new ParsingResult(false, e.Message);
            }
        }

        /// <summary>
        /// Runs the deferred per-user service init + legacy migration.  Safe to
        /// call from a BACKGROUND thread (it only does IPC / process launch / file
        /// IO; it does NOT touch the view model or any WPF object).  Returns true
        /// if the service is reachable afterwards (so the caller can decide whether
        /// a reload is worthwhile).  Intended to be invoked off the UI thread so the
        /// editor window is not blocked during UAC + MSIX deployment.
        /// </summary>
        public bool EnsureSettingsProvisioned()
        {
            TryBootstrapSettings();
            try
            {
                return ServiceProvisioner.IsServiceAvailable();
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static void TryBootstrapSettings()
        {
            try
            {
                SettingsBootstrapper.EnsureInitialized(new BootstrapRequest
                {
                    Reason = SettingsBootstrapper.TriggerReason.EditorOpened,
                    InstallFolder = PowerToysPathResolver.GetPowerToysInstallPath(),
                });
            }
            catch (Exception e)
            {
                // Best-effort: on failure reads fall back to the legacy file.
                Logger.LogWarning($"Settings bootstrap failed (continuing with fallback): {e.Message}");
            }
        }

        /// <summary>
        /// Save-time recovery: force a one-time provisioning attempt (bypassing the
        /// per-open sentinel) so a user who hasn't set up protection yet — or who
        /// previously declined the UAC — can enable it exactly when they try to save.
        /// Returns true iff the protected service is reachable afterwards.  Never
        /// writes plaintext; saving stays protected-only.
        /// </summary>
        private static bool TryProvisionProtectedStore()
        {
            try
            {
                SettingsBootstrapper.EnsureInitialized(new BootstrapRequest
                {
                    Reason = SettingsBootstrapper.TriggerReason.WorkspaceSaving,
                    InstallFolder = PowerToysPathResolver.GetPowerToysInstallPath(),
                });
                return ServiceProvisioner.IsServiceAvailable();
            }
            catch (Exception e)
            {
                Logger.LogWarning($"Save-time provisioning attempt failed: {e.Message}");
                return false;
            }
        }

        public Project ParseTempProject()
        {
            try
            {
                ProjectData parser = new();
                if (!File.Exists(TempProjectData.File))
                {
                    Logger.LogWarning($"ParseProject method. Workspaces storage file not found: {TempProjectData.File}");
                    return null;
                }

                Project project = new(parser.Read(TempProjectData.File));
                return project;
            }
            catch (Exception e)
            {
                Logger.LogError($"ParseProject method. Exception while parsing storage file: {e.Message}");
                return null;
            }
        }

        public void SerializeWorkspaces(List<Project> workspaces, bool useTempFile = false)
        {
            WorkspacesData serializer = new();
            WorkspacesData.WorkspacesListWrapper workspacesWrapper = new() { };
            workspacesWrapper.Workspaces = [];

            foreach (Project project in workspaces)
            {
                ProjectWrapper wrapper = new()
                {
                    Id = project.Id,
                    Name = project.Name,
                    CreationTime = project.CreationTime,
                    IsShortcutNeeded = project.IsShortcutNeeded,
                    MoveExistingWindows = project.MoveExistingWindows,
                    LastLaunchedTime = project.LastLaunchedTime,
                    Applications = [],
                    MonitorConfiguration = [],
                };

                foreach (Application app in project.Applications.Where(x => x.IsIncluded))
                {
                    wrapper.Applications.Add(new ApplicationWrapper
                    {
                        Id = app.Id,
                        Application = app.AppName,
                        ApplicationPath = app.AppPath,
                        Title = app.AppTitle,
                        PackageFullName = app.PackageFullName,
                        AppUserModelId = app.AppUserModelId,
                        PwaAppId = app.PwaAppId,
                        CommandLineArguments = app.CommandLineArguments,
                        IsElevated = app.IsElevated,
                        CanLaunchElevated = app.CanLaunchElevated,
                        Version = app.Version,
                        Maximized = app.Maximized,
                        Minimized = app.Minimized,
                        Position = new ApplicationWrapper.WindowPositionWrapper
                        {
                            X = app.Position.X,
                            Y = app.Position.Y,
                            Height = app.Position.Height,
                            Width = app.Position.Width,
                        },
                        Monitor = app.MonitorNumber,
                    });
                }

                foreach (MonitorSetup monitor in project.Monitors)
                {
                    wrapper.MonitorConfiguration.Add(new MonitorConfigurationWrapper
                    {
                        Id = monitor.MonitorName,
                        InstanceId = monitor.MonitorInstanceId,
                        MonitorNumber = monitor.MonitorNumber,
                        Dpi = monitor.Dpi,
                        MonitorRectDpiAware = new MonitorConfigurationWrapper.MonitorRectWrapper
                        {
                            Left = (int)monitor.MonitorDpiAwareBounds.Left,
                            Top = (int)monitor.MonitorDpiAwareBounds.Top,
                            Width = (int)monitor.MonitorDpiAwareBounds.Width,
                            Height = (int)monitor.MonitorDpiAwareBounds.Height,
                        },
                        MonitorRectDpiUnaware = new MonitorConfigurationWrapper.MonitorRectWrapper
                        {
                            Left = (int)monitor.MonitorDpiUnawareBounds.Left,
                            Top = (int)monitor.MonitorDpiUnawareBounds.Top,
                            Width = (int)monitor.MonitorDpiUnawareBounds.Width,
                            Height = (int)monitor.MonitorDpiUnawareBounds.Height,
                        },
                    });
                }

                workspacesWrapper.Workspaces.Add(wrapper);
            }

            try
            {
                string json = serializer.Serialize(workspacesWrapper);

                if (useTempFile)
                {
                    // Transient snapshot→editor handoff stays a direct user-writable
                    // file (not the protected store).
                    IOUtils ioUtils = new();
                    ioUtils.WriteFile(TempProjectData.File, json);
                    return;
                }

                // v6 (security): persist the settings through the service (PutBlob)
                // ONLY.  The protected %ProgramData% store is the single source of
                // truth for saves — there is deliberately NO unprotected plaintext
                // fallback: writing the user-writable %LocalAppData% file would
                // defeat the tamper protection (a same-user, non-elevated attacker
                // could then rewrite it).
                var payload = Encoding.UTF8.GetBytes(json);
                var rc = PTSettingsClient.PutBlob(payload);

                if (rc == PTSettingsClient.Result.Unavailable)
                {
                    // The protected service isn't set up yet (per-user install that
                    // was never elevated, or a previously declined/failed UAC).  Give
                    // the user the chance to enable protection right now (one elevation)
                    // and retry — but NEVER fall back to an unprotected write.
                    if (TryProvisionProtectedStore())
                    {
                        rc = PTSettingsClient.PutBlob(payload);
                    }
                }

                switch (rc)
                {
                    case PTSettingsClient.Result.Ok:
                        break;

                    case PTSettingsClient.Result.Unavailable:
                        // Protected service still not available (elevation declined /
                        // failed).  Fail safe: do NOT write plaintext.  Keep the edit
                        // in-window and tell the user how to enable protection.
                        Logger.LogError("Save blocked: protected settings service unavailable and no unprotected fallback is allowed.");
                        System.Windows.MessageBox.Show(
                            "PowerToys couldn't save your workspaces because the protected Workspaces settings service isn't set up yet. " +
                            "Workspaces are saved only to a protected, tamper-resistant location — there is no unprotected fallback. " +
                            "Setting it up needs a one-time administrator approval: reopen the Workspaces editor (or restart PowerToys) and accept the prompt, then save again. " +
                            "Your current changes are kept in this window until then.",
                            "Workspaces",
                            System.Windows.MessageBoxButton.OK,
                            System.Windows.MessageBoxImage.Warning);
                        break;

                    case PTSettingsClient.Result.AuthRejected:
                        // The service refused this app (e.g. version mismatch right
                        // after an update).  Do NOT silently drop the edit — tell the
                        // user and KEEP their changes in-window so they can retry once
                        // setup completes.  We deliberately do NOT write plaintext to
                        // the legacy file here (that would defeat the protection).
                        Logger.LogError("Save rejected by the settings service (AuthRejected).");
                        System.Windows.MessageBox.Show(
                            "PowerToys couldn't save your workspaces because the settings service didn't authorize this app. " +
                            "This can happen right after a PowerToys update — restart PowerToys (or reopen the Workspaces editor) to finish setup, then save again. " +
                            "Your current changes are kept in this window until then.",
                            "Workspaces",
                            System.Windows.MessageBoxButton.OK,
                            System.Windows.MessageBoxImage.Warning);
                        break;

                    default:
                        Logger.LogError($"Failed to save workspaces through the settings service: {rc}");
                        System.Windows.MessageBox.Show(
                            $"PowerToys couldn't save your workspaces (settings service error: {rc}). " +
                            "Your current changes are kept in this window — please try again.",
                            "Workspaces",
                            System.Windows.MessageBoxButton.OK,
                            System.Windows.MessageBoxImage.Warning);
                        break;
                }
            }
            catch (Exception e)
            {
                // TODO: show error
                Logger.LogError($"Exception while writing storage file: {e.Message}");
            }
        }

        private bool AddWorkspaces(MainViewModel mainViewModel, WorkspacesData.WorkspacesListWrapper workspaces)
        {
            foreach (ProjectWrapper project in workspaces.Workspaces)
            {
                mainViewModel.Workspaces.Add(new Project(project));
            }

            mainViewModel.Initialize();
            return true;
        }

        private bool SetWorkspaces(MainViewModel mainViewModel, WorkspacesData.WorkspacesListWrapper workspaces)
        {
            mainViewModel.Workspaces = [];
            return AddWorkspaces(mainViewModel, workspaces);
        }

        internal void SerializeTempProject(Project project)
        {
            SerializeWorkspaces([project], true);
        }
    }
}
