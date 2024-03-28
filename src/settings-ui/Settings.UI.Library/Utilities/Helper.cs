// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Net.NetworkInformation;
using System.Security.Principal;
using Microsoft.PowerToys.Settings.UI.Library.CustomAction;

namespace Microsoft.PowerToys.Settings.UI.Library.Utilities
{
    public static class Helper
    {
        public static readonly IFileSystem FileSystem = new FileSystem();

        public static string UserLocalAppDataPath { get; set; } = string.Empty;

        public static bool AllowRunnerToForeground()
        {
            var result = false;
            var processes = Process.GetProcessesByName("PowerToys");
            if (processes.Length > 0)
            {
                var pid = processes[0].Id;
                result = NativeMethods.AllowSetForegroundWindow(pid);
            }

            return result;
        }

        public static string GetSerializedCustomAction(string moduleName, string actionName, string actionValue)
        {
            var customAction = new CustomActionDataModel
            {
                Name = actionName,
                Value = actionValue,
            };

            var moduleCustomAction = new ModuleCustomAction
            {
                ModuleAction = customAction,
            };

            var sendCustomAction = new SendCustomAction(moduleName)
            {
                Action = moduleCustomAction,
            };

            return sendCustomAction.ToJsonString();
        }

        public static IFileSystemWatcher GetFileWatcher(string moduleName, string fileName, Action onChangedCallback)
        {
            var path = FileSystem.Path.Combine(LocalApplicationDataFolder(), $"Microsoft\\PowerToys\\{moduleName}");

            if (!FileSystem.Directory.Exists(path))
            {
                FileSystem.Directory.CreateDirectory(path);
            }

            var watcher = FileSystem.FileSystemWatcher.CreateNew();
            watcher.Path = path;
            watcher.Filter = fileName;
            watcher.NotifyFilter = NotifyFilters.LastWrite;
            watcher.EnableRaisingEvents = true;

            watcher.Changed += (o, e) => onChangedCallback();

            return watcher;
        }

        public static string LocalApplicationDataFolder()
        {
            WindowsIdentity currentUser = WindowsIdentity.GetCurrent();
            SecurityIdentifier currentUserSID = currentUser.User;

            SecurityIdentifier localSystemSID = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);
            if (currentUserSID.Equals(localSystemSID) && UserLocalAppDataPath != string.Empty)
            {
                return UserLocalAppDataPath;
            }
            else
            {
                return Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            }
        }

        public static string GetPowerToysInstallationFolder()
        {
            // PowerToys.exe is in the parent folder relative to Settings.
            var settingsPath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            return Directory.GetParent(settingsPath).FullName;
        }

        private static readonly interop.LayoutMapManaged LayoutMap = new interop.LayoutMapManaged();

        public static string GetKeyName(uint key)
        {
            return LayoutMap.GetKeyName(key);
        }

        public static uint GetKeyValue(string key)
        {
            return LayoutMap.GetKeyValue(key);
        }

        public static string GetProductVersion()
        {
            return interop.CommonManaged.GetProductVersion();
        }

        public static int CompareVersions(string version1, string version2)
        {
            try
            {
                // Split up the version strings into int[]
                // Example: v10.0.2 -> {10, 0, 2};
                ArgumentNullException.ThrowIfNull(version1);
                ArgumentNullException.ThrowIfNull(version2);

                var v1 = version1.Substring(1).Split('.').Select(int.Parse).ToArray();
                var v2 = version2.Substring(1).Split('.').Select(int.Parse).ToArray();

                if (v1.Length != 3 || v2.Length != 3)
                {
                    throw new FormatException();
                }

                if (v1[0] - v2[0] != 0)
                {
                    return v1[0] - v2[0];
                }

                if (v1[1] - v2[1] != 0)
                {
                    return v1[1] - v2[1];
                }

                return v1[2] - v2[2];
            }
            catch (Exception)
            {
                throw new FormatException("Bad product version format");
            }
        }

        public const uint VirtualKeyWindows = interop.Constants.VK_WIN_BOTH;
    }
}
