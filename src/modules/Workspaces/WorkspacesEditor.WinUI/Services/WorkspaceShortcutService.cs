// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using ManagedCommon;
using WorkspacesCsharpLibrary.Data;
using WorkspacesCsharpLibrary.Utils;
using WorkspacesEditor.Helpers;
using WorkspacesEditor.Models;
using WorkspacesEditor.Utils;

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

        private enum ShortcutState
        {
            Missing,
            Owned,
            OtherOrUnreadable,
        }

        public bool Exists(Project project)
        {
            try
            {
                return Guid.TryParse(project?.Id, out Guid workspaceId) &&
                    GetShortcutState(GetDesktopShortcutAddress(project.Name), workspaceId) == ShortcutState.Owned;
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

                string shortcutAddress = GetDesktopShortcutAddress(project.Name);
                string previousAddress = previousName == null ? shortcutAddress : GetDesktopShortcutAddress(previousName);
                bool renamed = !string.Equals(shortcutAddress, previousAddress, StringComparison.OrdinalIgnoreCase);
                string shortcutIconFilename = GetShortcutStoreAddress(project);

                if (!project.IsShortcutNeeded)
                {
                    bool currentRemoved = TryRemoveOwnedShortcut(shortcutAddress, workspaceId);
                    bool previousRemoved = !renamed || TryRemoveOwnedShortcut(previousAddress, workspaceId);
                    if (currentRemoved && previousRemoved)
                    {
                        RemoveIcon(shortcutIconFilename);
                    }

                    return;
                }

                if (GetShortcutState(shortcutAddress, workspaceId) == ShortcutState.OtherOrUnreadable)
                {
                    Logger.LogWarning("The workspace shortcut name is already in use by another or unreadable link.");
                    return;
                }

                SaveIcon(project, shortcutIconFilename);
                SaveShortcut(workspaceId, shortcutAddress, shortcutIconFilename);

                if (renamed)
                {
                    // Both names use the same per-workspace icon; retire only the old link.
                    TryRemoveOwnedShortcut(previousAddress, workspaceId);
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
                if (Guid.TryParse(project?.Id, out Guid workspaceId) &&
                    TryRemoveOwnedShortcut(GetDesktopShortcutAddress(project.Name), workspaceId))
                {
                    RemoveIcon(GetShortcutStoreAddress(project));
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to remove the workspace shortcut", ex);
            }
        }

        private ShortcutState GetShortcutState(string shortcutAddress, Guid workspaceId)
        {
            IWshRuntimeLibrary.WshShell shell = null;
            IWshRuntimeLibrary.IWshShortcut shortcut = null;
            try
            {
                if ((File.GetAttributes(shortcutAddress) & FileAttributes.Directory) != 0)
                {
                    return ShortcutState.OtherOrUnreadable;
                }

                shell = new IWshRuntimeLibrary.WshShell();
                shortcut = (IWshRuntimeLibrary.IWshShortcut)shell.CreateShortcut(shortcutAddress);
                string[] arguments = shortcut.Arguments.Split(' ', StringSplitOptions.RemoveEmptyEntries);

                // Recognize only the generated launcher arguments. In particular, never infer
                // ownership from a stale Description after the target workspace was edited.
                bool owned = string.Equals(Path.GetFileName(shortcut.TargetPath), LauncherFileName, StringComparison.OrdinalIgnoreCase) &&
                    arguments.Length == 2 &&
                    Guid.TryParseExact(arguments[0], "B", out Guid shortcutWorkspaceId) && shortcutWorkspaceId == workspaceId &&
                    arguments[1] == ((int)InvokePoint.Shortcut).ToString(CultureInfo.InvariantCulture);
                return owned ? ShortcutState.Owned : ShortcutState.OtherOrUnreadable;
            }
            catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
            {
                return ShortcutState.Missing;
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to read the workspace shortcut", ex);
                return ShortcutState.OtherOrUnreadable;
            }
            finally
            {
                ReleaseComObject(shortcut);
                ReleaseComObject(shell);
            }
        }

        private bool TryRemoveOwnedShortcut(string shortcutAddress, Guid workspaceId)
        {
            ShortcutState state = GetShortcutState(shortcutAddress, workspaceId);
            if (state == ShortcutState.Missing)
            {
                return true;
            }

            if (state != ShortcutState.Owned)
            {
                return false;
            }

            try
            {
                // The probe releases its COM objects before the file is deleted.
                File.Delete(shortcutAddress);
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to delete the workspace shortcut link", ex);
                return false;
            }
        }

        private static void SaveIcon(Project project, string shortcutIconFilename)
        {
            string iconFolder = Path.GetDirectoryName(shortcutIconFilename);
            Directory.CreateDirectory(iconFolder);
            string temporaryIconFilename = Path.Combine(iconFolder, $"{Guid.NewGuid():N}.tmp.ico");
            try
            {
                using var icon = WorkspacesIcon.DrawIcon(WorkspacesIcon.IconTextFromProjectName(project.Name), ThemeHelper.IsDarkTheme());
                WorkspacesIcon.SaveIcon(icon, temporaryIconFilename);
                File.Move(temporaryIconFilename, shortcutIconFilename, overwrite: true);
            }
            finally
            {
                File.Delete(temporaryIconFilename);
            }
        }

        private void SaveShortcut(Guid workspaceId, string shortcutAddress, string shortcutIconFilename)
        {
            IWshRuntimeLibrary.WshShell shell = null;
            IWshRuntimeLibrary.IWshShortcut shortcut = null;
            try
            {
                string launcherPath = GetAbsolutePath(_launcherPath);
                string canonicalId = workspaceId.ToString("B");
                shell = new IWshRuntimeLibrary.WshShell();
                shortcut = (IWshRuntimeLibrary.IWshShortcut)shell.CreateShortcut(shortcutAddress);
                shortcut.Description = $"Project Launcher {canonicalId}";
                shortcut.TargetPath = launcherPath;
                shortcut.Arguments = $"{canonicalId} {(int)InvokePoint.Shortcut}";
                shortcut.WorkingDirectory = Path.GetDirectoryName(launcherPath);
                shortcut.IconLocation = $"\"{shortcutIconFilename}\",0";
                shortcut.Save();
            }
            finally
            {
                ReleaseComObject(shortcut);
                ReleaseComObject(shell);
            }
        }

        private string GetDesktopShortcutAddress(string name) => Path.GetFullPath(Path.Combine(GetAbsolutePath(_desktopFolder), name + ".lnk"));

        private string GetShortcutStoreAddress(Project project) => Path.Combine(GetAbsolutePath(_iconFolder), project.Id + ".ico");

        private static string GetAbsolutePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path))
            {
                throw new DirectoryNotFoundException("The workspace shortcut path is unavailable.");
            }

            return Path.GetFullPath(path);
        }

        private static void RemoveIcon(string iconFilename)
        {
            if (File.Exists(iconFilename))
            {
                File.Delete(iconFilename);
            }
        }

        private static void ReleaseComObject(object value)
        {
            if (value != null && Marshal.IsComObject(value))
            {
                Marshal.FinalReleaseComObject(value);
            }
        }
    }
}
