// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using ManagedCommon;
using WorkspacesCsharpLibrary.Utils;
using WorkspacesEditor.Helpers;
using WorkspacesEditor.Models;
using WorkspacesEditor.Utils;
using SharingRetryBudget = WorkspacesEditor.Services.WorkspaceShortcutFile.SharingRetryBudget;

namespace WorkspacesEditor.Services
{
    internal sealed class WorkspaceShortcutService : IWorkspaceShortcutService
    {
        private const string LauncherFileName = "PowerToys.WorkspacesLauncher.exe";
        private readonly string _desktopFolder;
        private readonly string _iconFolder;
        private readonly string _launcherPath;

        public WorkspaceShortcutService()
            : this(
                FolderUtils.Desktop(),
                Path.Combine(FolderUtils.DataFolder(), "WorkspacesIcons"),
                Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(Environment.ProcessPath)), LauncherFileName))
        {
        }

        internal WorkspaceShortcutService(string desktopFolder, string iconFolder, string launcherPath)
        {
            _desktopFolder = desktopFolder;
            _iconFolder = iconFolder;
            _launcherPath = launcherPath;
        }

        public bool Exists(Project project) => GetPresence(project) == true;

        public bool? GetPresence(Project project)
        {
            try
            {
                if (!Guid.TryParse(project?.Id, out Guid workspaceId))
                {
                    return false;
                }

                var retry = new SharingRetryBudget();
                var current = Inspect(project.Name, workspaceId, retry);
                if (current.State == ShortcutState.Owned)
                {
                    return true;
                }

                bool unreadable = current.State == ShortcutState.Unreadable;
                foreach (string name in ReadJournal(project.Id, workspaceId).Names)
                {
                    if (name == project.Name)
                    {
                        continue;
                    }

                    var entry = Inspect(name, workspaceId, retry);
                    if (entry.State == ShortcutState.Owned)
                    {
                        return true;
                    }

                    unreadable |= entry.State == ShortcutState.Unreadable;
                }

                return unreadable ? null : false;
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to check the workspace shortcut", ex);
                return null;
            }
        }

        public bool TryPrepareChange(Project original, bool deleting)
        {
            try
            {
                if (Guid.TryParse(original?.Id, out Guid workspaceId))
                {
                    RememberOriginal(ReadJournal(original.Id, workspaceId), workspaceId, original.Name, deleting, new SharingRetryBudget());
                }

                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to record the workspace shortcut before changing the workspace", ex);
                return false;
            }
        }

        public void Apply(Project project, string previousName = null) => ApplyWithResult(project, previousName);

        public WorkspaceShortcutResult ApplyWithResult(Project project, string previousName = null)
        {
            bool created = false;
            try
            {
                if (!Guid.TryParse(project?.Id, out Guid workspaceId))
                {
                    return new(WorkspaceShortcutStatus.Failed);
                }

                var retry = new SharingRetryBudget();
                var journal = ReadJournal(project.Id, workspaceId);
                if (previousName != null && previousName != project.Name)
                {
                    RememberOriginal(journal, workspaceId, previousName, deleting: false, retry);
                }

                if (!project.IsShortcutNeeded)
                {
                    var candidates = journal.Names;
                    AddName(candidates, previousName);
                    AddName(candidates, project.Name);
                    return RemoveLinks(journal, workspaceId, candidates, deleting: false, retry, project.Name, previousName);
                }

                if (!WorkspaceShortcutJournal.IsValidName(project.Name))
                {
                    return new(WorkspaceShortcutStatus.Failed);
                }

                var destination = Inspect(project.Name, workspaceId, retry);
                if (destination.State is ShortcutState.Foreign or ShortcutState.Unverifiable)
                {
                    return new(WorkspaceShortcutStatus.NameConflict);
                }

                if (destination.State == ShortcutState.Unreadable)
                {
                    return new(WorkspaceShortcutStatus.Failed);
                }

                var names = journal.Names;
                byte[] template = null;
                if (destination.State == ShortcutState.Missing)
                {
                    template = FindTemplate(names, previousName, project.Name, workspaceId, retry);
                }

                // Both possible desktop names must be recoverable before publishing or retiring either link.
                names.Add(project.Name);
                journal.Save(names, pendingRemoval: false);
                string iconPath = GetIconPath(journal);
                string keptName;
                try
                {
                    keptName = Publish(project, workspaceId, destination.State, template, iconPath, retry);
                }
                catch (Win32Exception ex) when (destination.State == ShortcutState.Missing && ex.NativeErrorCode is 80 or 183 &&
                    Inspect(project.Name, workspaceId, retry).State == ShortcutState.Foreign)
                {
                    return new(WorkspaceShortcutStatus.NameConflict);
                }

                created = destination.State == ShortcutState.Missing;
                var (remaining, failed) = CleanLinks(names, workspaceId, iconPath, keptName, retry);
                remaining.Add(Path.GetFileNameWithoutExtension(keptName));
                journal.Save(remaining, pendingRemoval: false);
                return new(failed ? WorkspaceShortcutStatus.Failed : WorkspaceShortcutStatus.Success, created);
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to update the workspace shortcut", ex);
                return new(WorkspaceShortcutStatus.Failed, created);
            }
        }

        public void Remove(Project project) => RemoveWithResult(project);

        public WorkspaceShortcutResult RemoveWithResult(Project project)
        {
            try
            {
                if (!Guid.TryParse(project?.Id, out Guid workspaceId))
                {
                    return new(WorkspaceShortcutStatus.Success);
                }

                var retry = new SharingRetryBudget();
                var journal = ReadJournal(project.Id, workspaceId);
                RememberOriginal(journal, workspaceId, project.Name, deleting: true, retry);
                var names = journal.Names;
                AddName(names, project.Name);
                return RemoveLinks(journal, workspaceId, names, deleting: true, retry, project.Name);
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to remove the workspace shortcut", ex);
                return new(WorkspaceShortcutStatus.Failed);
            }
        }

        public bool RetryPendingRemovals(IEnumerable<string> activeWorkspaceIds)
        {
            ArgumentNullException.ThrowIfNull(activeWorkspaceIds);
            bool retryNeeded = false;
            try
            {
                var active = new HashSet<Guid>();
                foreach (string value in activeWorkspaceIds)
                {
                    if (Guid.TryParse(value, out Guid id))
                    {
                        active.Add(id);
                    }
                }

                var retry = new SharingRetryBudget();
                foreach (Guid id in WorkspaceShortcutJournal.EnumerateIds(GetAbsolutePath(_iconFolder)))
                {
                    if (active.Contains(id))
                    {
                        // Deleting the workspace may have failed after its shortcut was journaled.
                        continue;
                    }

                    try
                    {
                        var journal = ReadJournal(id.ToString("B"), id);
                        if (journal.PendingRemoval)
                        {
                            retryNeeded |= RemoveLinks(journal, id, journal.Names, deleting: true, retry).Status == WorkspaceShortcutStatus.Failed;
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError("Failed to retry a workspace shortcut removal", ex);
                        retryNeeded |= ex is IOException or UnauthorizedAccessException or Win32Exception;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to read pending workspace shortcut removals", ex);
                retryNeeded = true;
            }

            return retryNeeded;
        }

        private void RememberOriginal(WorkspaceShortcutJournal journal, Guid workspaceId, string name, bool deleting, SharingRetryBudget retry)
        {
            var names = journal.Names;
            if (WorkspaceShortcutJournal.IsValidName(name) && !names.Contains(name))
            {
                var entry = Inspect(name, workspaceId, retry);
                string iconPath = GetIconPath(journal);
                if (entry.State is ShortcutState.Owned or ShortcutState.Unreadable ||
                    (entry.State == ShortcutState.Unverifiable && HasIcon(iconPath)) ||
                    WorkspaceShortcutFile.MayReferenceIcon(entry.IconPath, iconPath) || (deleting && HasIcon(iconPath)))
                {
                    names.Add(name);
                }
            }

            // Do not create recovery data just to remove a workspace which never had a shortcut.
            journal.Save(names, pendingRemoval: deleting && (names.Count != 0 || journal.PendingRemoval));
        }

        private byte[] FindTemplate(HashSet<string> registered, string previousName, string requestedName, Guid workspaceId, SharingRetryBudget retry)
        {
            bool unreadable = false;
            foreach (string name in registered.Prepend(previousName).Distinct(StringComparer.Ordinal))
            {
                if (name == null || name == requestedName)
                {
                    continue;
                }

                var entry = Inspect(name, workspaceId, retry);
                if (entry.State == ShortcutState.Owned)
                {
                    return entry.Contents;
                }

                unreadable |= entry.State == ShortcutState.Unreadable;
            }

            if (unreadable)
            {
                throw new IOException("The previous workspace shortcut could not be read for the rename.");
            }

            return null;
        }

        private string Publish(Project project, Guid workspaceId, ShortcutState state, byte[] template, string iconPath, SharingRetryBudget retry)
        {
            string shortcutPath = GetDesktopPath(project.Name);
            string launcherPath = GetAbsolutePath(_launcherPath);
            string iconFolder = Path.GetDirectoryName(iconPath);
            Directory.CreateDirectory(iconFolder);
            string temporaryIcon = Path.Combine(iconFolder, $"{Guid.NewGuid():N}.tmp.ico");
            try
            {
                using var icon = WorkspacesIcon.DrawIcon(WorkspacesIcon.IconTextFromProjectName(project.Name), ThemeHelper.IsDarkTheme());
                WorkspacesIcon.SaveIcon(icon, temporaryIcon);

                void CommitIcon() => File.Move(temporaryIcon, iconPath, overwrite: true);

                if (state == ShortcutState.Missing)
                {
                    WorkspaceShortcutFile.Create(shortcutPath, WorkspaceShellLink.Create(launcherPath, workspaceId, iconPath, template), CommitIcon);
                    return Path.GetFileName(shortcutPath);
                }

                return WorkspaceShortcutFile.Update(shortcutPath, launcherPath, workspaceId, iconPath, CommitIcon, retry);
            }
            finally
            {
                File.Delete(temporaryIcon);
            }
        }

        private WorkspaceShortcutResult RemoveLinks(WorkspaceShortcutJournal journal, Guid workspaceId, HashSet<string> names, bool deleting, SharingRetryBudget retry, string currentName = null, string previousName = null)
        {
            if (names.Count == 0 && !journal.PendingRemoval)
            {
                return new(WorkspaceShortcutStatus.Success);
            }

            string iconPath = GetIconPath(journal);
            var (remaining, failed) = CleanLinks(names, workspaceId, iconPath, null, retry, currentName, previousName);
            if (remaining.Count == 0 && HasIcon(iconPath) && !journal.HasUnrecoverableNames)
            {
                File.Delete(iconPath);
            }

            journal.Save(remaining, pendingRemoval: deleting && remaining.Count != 0);
            return new(failed ? WorkspaceShortcutStatus.Failed : WorkspaceShortcutStatus.Success);
        }

        private (HashSet<string> Remaining, bool Failed) CleanLinks(IEnumerable<string> names, Guid workspaceId, string iconPath, string keepName, SharingRetryBudget retry, string currentName = null, string previousName = null)
        {
            var remaining = new HashSet<string>(StringComparer.Ordinal);
            var foreign = new List<string>();
            bool removedOwned = false;
            bool failed = false;
            foreach (string name in names)
            {
                if (name + ".lnk" == keepName)
                {
                    continue;
                }

                var entry = Inspect(name, workspaceId, retry);
                switch (entry.State)
                {
                    case ShortcutState.Owned:
                        try
                        {
                            WorkspaceShortcutFile.Remove(GetDesktopPath(name), workspaceId, keepName, retry);
                            removedOwned = true;
                        }
                        catch (Exception ex)
                        {
                            Logger.LogError("Failed to retire the workspace shortcut", ex);
                            remaining.Add(name);
                            failed = true;
                        }

                        break;

                    case ShortcutState.Foreign:
                        if (name == currentName || name == previousName)
                        {
                            foreign.Add(name);
                        }

                        if (WorkspaceShortcutFile.MayReferenceIcon(entry.IconPath, iconPath))
                        {
                            // A user retargeted the old link but kept its workspace icon.
                            remaining.Add(name);
                        }

                        break;

                    case ShortcutState.Unreadable:
                        remaining.Add(name);
                        failed = true;
                        break;

                    case ShortcutState.Unverifiable:
                        // Malformed or over-limit content cannot establish ownership or icon use.
                        // Preserve it, but no timed retry can change the bytes already inspected.
                        if (HasIcon(iconPath))
                        {
                            remaining.Add(name);
                        }

                        break;
                }
            }

            if (keepName == null && !removedOwned && remaining.Count == 0 && foreign.Count > 0 && HasIcon(iconPath))
            {
                // A foreign replacement at the only known name does not authorize removal of the icon.
                remaining.UnionWith(foreign);
            }

            return (remaining, failed);
        }

        private (ShortcutState State, string IconPath, byte[] Contents) Inspect(string name, Guid workspaceId, SharingRetryBudget retry)
        {
            if (!WorkspaceShortcutJournal.IsValidName(name))
            {
                return (ShortcutState.Missing, null, null);
            }

            try
            {
                string path = GetDesktopPath(name);
                FileAttributes attributes = File.GetAttributes(path);
                if ((attributes & FileAttributes.Directory) != 0)
                {
                    return (ShortcutState.Foreign, null, null);
                }

                if ((attributes & FileAttributes.ReparsePoint) != 0 && new FileInfo(path).LinkTarget != null)
                {
                    return (ShortcutState.Unreadable, null, null);
                }

                var (owned, iconPath) = WorkspaceShortcutFile.Inspect(path, workspaceId, retry, out byte[] contents);
                return (owned ? ShortcutState.Owned : ShortcutState.Foreign, iconPath, contents);
            }
            catch (FileNotFoundException)
            {
                return (ShortcutState.Missing, null, null);
            }
            catch (InvalidDataException)
            {
                return (ShortcutState.Unverifiable, null, null);
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to read the workspace shortcut", ex);
                return (ShortcutState.Unreadable, null, null);
            }
        }

        private WorkspaceShortcutJournal ReadJournal(string projectId, Guid workspaceId) =>
            WorkspaceShortcutJournal.Read(GetAbsolutePath(_iconFolder), workspaceId, projectId);

        private string GetDesktopPath(string name) => Path.Combine(GetAbsolutePath(_desktopFolder), name + ".lnk");

        private string GetIconPath(WorkspaceShortcutJournal journal) => Path.Combine(GetAbsolutePath(_iconFolder), journal.ProjectId + ".ico");

        private static bool HasIcon(string iconPath)
        {
            try
            {
                return (File.GetAttributes(iconPath) & FileAttributes.Directory) == 0;
            }
            catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
            {
                return false;
            }
        }

        private static void AddName(HashSet<string> names, string name)
        {
            if (WorkspaceShortcutJournal.IsValidName(name))
            {
                names.Add(name);
            }
        }

        private static string GetAbsolutePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path))
            {
                throw new DirectoryNotFoundException("The workspace shortcut path is unavailable.");
            }

            return Path.GetFullPath(path);
        }

        private enum ShortcutState
        {
            Missing,
            Owned,
            Foreign,
            Unverifiable,
            Unreadable,
        }
    }
}
