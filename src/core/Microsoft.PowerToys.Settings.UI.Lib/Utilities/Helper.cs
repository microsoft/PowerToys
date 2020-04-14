// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.PowerToys.Settings.UI.Lib.CustomAction;

namespace Microsoft.PowerToys.Settings.UI.Lib.Utilities
{
    public class Helper
    {
        public static bool AllowRunnerToForeground()
        {
            var result = false;
            var processes = Process.GetProcessesByName("PowerToys");
            if (processes.Length > 0)
            {
                var pid = processes[0].Id;
                result = AllowSetForegroundWindow(pid);
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

            var sendCustomAction = new SendCustomAction(moduleName);
            sendCustomAction.Action = moduleCustomAction;
            return sendCustomAction.ToJsonString();
        }

        public static FileSystemWatcher GetFileWatcher(string moduleName, string fileName, Action onChangedCallback)
        {
            var watcher = new FileSystemWatcher();
            watcher.Path = Path.Combine(LocalApplicationDataFolder(), $"Microsoft\\PowerToys\\{moduleName}");
            watcher.Filter = fileName;
            watcher.NotifyFilter = NotifyFilters.LastWrite;
            watcher.Changed += (o, e) => onChangedCallback();
            watcher.EnableRaisingEvents = true;

            return watcher;
        }

        private static string LocalApplicationDataFolder()
        {
            return Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        }

        [DllImport("user32.dll")]
        private static extern bool AllowSetForegroundWindow(int dwProcessId);
    }
}
