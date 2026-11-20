// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using ManagedCommon;
using WorkspacesCsharpLibrary.Utils;
using WorkspacesEditor.Helpers;
using WorkspacesEditor.Models;
using WorkspacesEditor.Utils;

namespace WorkspacesEditor.Services
{
    internal sealed partial class WorkspaceShortcutService : IWorkspaceShortcutService
    {
        private const string LauncherFileName = "PowerToys.WorkspacesLauncher.exe";
        private static readonly char[] InvalidNameChars = Path.GetInvalidFileNameChars();
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

        public bool Exists(Project project)
        {
            try
            {
                if (!Guid.TryParse(project?.Id, out Guid workspaceId))
                {
                    return false;
                }

                if (IsValidName(project.Name) && Inspect(project.Name, workspaceId).State == ShortcutState.Owned)
                {
                    return true;
                }

                return ReadNames(workspaceId).Any(name => name != project.Name && Inspect(name, workspaceId).State == ShortcutState.Owned);
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to check the workspace shortcut", ex);
                return false;
            }
        }

        public void Apply(Project project, string previousName = null)
        {
            try
            {
                if (!Guid.TryParse(project?.Id, out Guid workspaceId))
                {
                    return;
                }

                var names = ReadNames(workspaceId);
                if (previousName != project.Name && IsValidName(previousName) && !names.Contains(previousName) &&
                    Inspect(previousName, workspaceId).State is ShortcutState.Owned or ShortcutState.Unreadable)
                {
                    // Older installations have no record. Keep the old name before attempting the rename.
                    names.Add(previousName);
                    WriteNames(workspaceId, names);
                }

                if (!project.IsShortcutNeeded)
                {
                    Remove(project, workspaceId, names, previousName);
                    return;
                }

                if (!IsValidName(project.Name))
                {
                    Logger.LogWarning("The workspace name cannot be used as a desktop shortcut filename.");
                    return;
                }

                ShortcutState state = Inspect(project.Name, workspaceId).State;
                if (state is ShortcutState.Foreign or ShortcutState.Unreadable)
                {
                    Logger.LogWarning("The workspace shortcut name is already in use.");
                    return;
                }

                string iconPath = GetIconPath(project);
                string keptName = Publish(project, workspaceId, state, iconPath);
                var (remaining, _) = CleanLinks(names, workspaceId, iconPath, keptName);
                remaining.Add(Path.GetFileNameWithoutExtension(keptName));
                if (!names.SetEquals(remaining))
                {
                    WriteNames(workspaceId, remaining);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to update the workspace shortcut", ex);
            }
        }

        public void Remove(Project project)
        {
            try
            {
                if (Guid.TryParse(project?.Id, out Guid workspaceId))
                {
                    Remove(project, workspaceId, ReadNames(workspaceId));
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to remove the workspace shortcut", ex);
            }
        }

        private string Publish(Project project, Guid workspaceId, ShortcutState state, string iconPath)
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
                    WorkspaceShortcutFile.Create(shortcutPath, WorkspaceShellLink.Create(launcherPath, workspaceId, iconPath), CommitIcon);
                    return Path.GetFileName(shortcutPath);
                }

                return WorkspaceShortcutFile.Update(shortcutPath, launcherPath, workspaceId, iconPath, CommitIcon);
            }
            finally
            {
                File.Delete(temporaryIcon);
            }
        }

        private void Remove(Project project, Guid workspaceId, HashSet<string> names, string previousName = null)
        {
            var candidates = new HashSet<string>(names, StringComparer.Ordinal);
            if (IsValidName(project.Name))
            {
                candidates.Add(project.Name);
            }

            if (IsValidName(previousName))
            {
                candidates.Add(previousName);
            }

            if (candidates.Count == 0)
            {
                return;
            }

            string iconPath = GetIconPath(project);
            var (remaining, canDeleteIcon) = CleanLinks(candidates, workspaceId, iconPath, null);
            if (canDeleteIcon && File.Exists(iconPath))
            {
                File.Delete(iconPath);
            }

            if (!names.SetEquals(remaining))
            {
                WriteNames(workspaceId, remaining);
            }
        }

        private (HashSet<string> Remaining, bool CanDeleteIcon) CleanLinks(IEnumerable<string> names, Guid workspaceId, string iconPath, string keepName)
        {
            var remaining = new HashSet<string>(StringComparer.Ordinal);
            bool removedOwned = false;
            bool foundForeign = false;
            bool iconInUse = false;
            foreach (string name in names)
            {
                if (name + ".lnk" == keepName)
                {
                    continue;
                }

                var (state, linkIcon) = Inspect(name, workspaceId);
                switch (state)
                {
                    case ShortcutState.Owned:
                        try
                        {
                            WorkspaceShortcutFile.Remove(GetDesktopPath(name), workspaceId, keepName);
                            removedOwned = true;
                        }
                        catch (Exception ex)
                        {
                            Logger.LogError("Failed to retire the workspace shortcut", ex);
                            remaining.Add(name);
                        }

                        break;

                    case ShortcutState.Foreign:
                        foundForeign = true;
                        iconInUse |= string.Equals(linkIcon?.Trim('"'), iconPath, StringComparison.OrdinalIgnoreCase);
                        break;

                    case ShortcutState.Unreadable:
                        remaining.Add(name);
                        break;
                }
            }

            return (remaining, remaining.Count == 0 && !iconInUse && (removedOwned || !foundForeign));
        }

        private (ShortcutState State, string IconPath) Inspect(string name, Guid workspaceId)
        {
            try
            {
                string path = GetDesktopPath(name);
                FileAttributes attributes = File.GetAttributes(path);
                if ((attributes & FileAttributes.Directory) != 0)
                {
                    return (ShortcutState.Foreign, null);
                }

                if ((attributes & FileAttributes.ReparsePoint) != 0 && new FileInfo(path).LinkTarget != null)
                {
                    return (ShortcutState.Unreadable, null);
                }

                var (owned, iconPath) = WorkspaceShortcutFile.Inspect(path, workspaceId);
                return (owned ? ShortcutState.Owned : ShortcutState.Foreign, iconPath);
            }
            catch (FileNotFoundException)
            {
                return (ShortcutState.Missing, null);
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to read the workspace shortcut", ex);
                return (ShortcutState.Unreadable, null);
            }
        }

        private string GetDesktopPath(string name) => Path.Combine(GetAbsolutePath(_desktopFolder), name + ".lnk");

        private string GetIconPath(Project project) => Path.Combine(GetAbsolutePath(_iconFolder), project.Id + ".ico");

        private string GetNamesPath(Guid workspaceId) => Path.Combine(GetAbsolutePath(_iconFolder), $"{workspaceId:B}.links.json");

        private HashSet<string> ReadNames(Guid workspaceId)
        {
            try
            {
                string[] names = JsonSerializer.Deserialize<string[]>(File.ReadAllText(GetNamesPath(workspaceId)));
                return new HashSet<string>((names ?? Array.Empty<string>()).Where(IsValidName), StringComparer.Ordinal);
            }
            catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
            {
                return new HashSet<string>(StringComparer.Ordinal);
            }
            catch (JsonException ex)
            {
                Logger.LogError("Failed to read the workspace shortcut names", ex);
                return new HashSet<string>(StringComparer.Ordinal);
            }
        }

        private void WriteNames(Guid workspaceId, HashSet<string> names)
        {
            string path = GetNamesPath(workspaceId);
            if (names.Count == 0)
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }

                return;
            }

            string directory = Path.GetDirectoryName(path);
            Directory.CreateDirectory(directory);
            string temporary = Path.Combine(directory, Path.GetRandomFileName());
            try
            {
                File.WriteAllText(temporary, JsonSerializer.Serialize(names));
                File.Move(temporary, path, overwrite: true);
            }
            finally
            {
                File.Delete(temporary);
            }
        }

        private static bool IsValidName(string name) =>
            !string.IsNullOrWhiteSpace(name) && name is not "." and not ".." &&
            name.IndexOfAny(InvalidNameChars) < 0 && !ReservedDeviceName().IsMatch(name);

        [GeneratedRegex(@"^(CON|PRN|AUX|NUL|CONIN\$|CONOUT\$|(?:COM|LPT)[1-9¹²³]) *(?:[.]|$)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
        private static partial Regex ReservedDeviceName();

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
            Unreadable,
        }
    }
}
