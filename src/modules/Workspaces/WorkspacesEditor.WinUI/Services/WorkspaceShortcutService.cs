// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
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
        public bool Exists(Project project) => File.Exists(GetDesktopShortcutAddress(project));

        public void Apply(Project project)
        {
            if (!project.IsShortcutNeeded)
            {
                Remove(project);
                return;
            }

            try
            {
                var basePath = Path.GetDirectoryName(Path.GetDirectoryName(Environment.ProcessPath));
                var shortcutAddress = GetDesktopShortcutAddress(project);
                var shortcutIconFilename = GetShortcutStoreAddress(project);

                using var icon = WorkspacesIcon.DrawIcon(WorkspacesIcon.IconTextFromProjectName(project.Name), ThemeHelper.IsDarkTheme());
                WorkspacesIcon.SaveIcon(icon, shortcutIconFilename);

                File.WriteAllBytes(shortcutAddress, Array.Empty<byte>());

                Shell32.Shell shell = new();
                Shell32.Folder directory = shell.NameSpace(FolderUtils.Desktop());
                Shell32.FolderItem folderItem = directory.Items().Item($"{project.Name}.lnk");
                Shell32.ShellLinkObject link = (Shell32.ShellLinkObject)folderItem.GetLink;

                link.Description = $"Project Launcher {project.Id}";
                link.Path = Path.Combine(basePath, "PowerToys.WorkspacesLauncher.exe");
                link.Arguments = $"{project.Id} {(int)InvokePoint.Shortcut}";
                link.WorkingDirectory = basePath;
                link.SetIconLocation(shortcutIconFilename, 0);
                link.Save(shortcutAddress);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Shortcut creation error: {ex.Message}");
            }
        }

        public void Remove(Project project)
        {
            string shortcutAddress = GetDesktopShortcutAddress(project);
            string shortcutIconFilename = GetShortcutStoreAddress(project);

            if (File.Exists(shortcutIconFilename))
            {
                File.Delete(shortcutIconFilename);
            }

            if (File.Exists(shortcutAddress))
            {
                File.Delete(shortcutAddress);
            }
        }

        private static string GetDesktopShortcutAddress(Project project) => Path.Combine(FolderUtils.Desktop(), project.Name + ".lnk");

        private static string GetShortcutStoreAddress(Project project)
        {
            var dataFolder = FolderUtils.DataFolder();
            Directory.CreateDirectory(dataFolder);
            var shortcutStoreFolder = Path.Combine(dataFolder, "WorkspacesIcons");
            Directory.CreateDirectory(shortcutStoreFolder);
            return Path.Combine(shortcutStoreFolder, project.Id + ".ico");
        }
    }
}
