// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.IO;
using ManagedCommon;

namespace Common.UI
{
    public static class SettingsDeepLink
    {
        public enum SettingsWindow
        {
            Overview = 0,
            Awake,
            ColorPicker,
            FancyZones,
            Run,
            ImageResizer,
            KBM,
            MouseUtils,
            PowerRename,
            FileExplorer,
            ShortcutGuide,
            Hosts,
            MeasureTool,
            PowerOCR,
            RegistryPreview,
            CropAndLock,
            EnvironmentVariables,
            Dashboard,
            AdvancedPaste,
            Workspaces,
            CmdPal,
            ZoomIt,
            AlwaysOnTop,
            FileLockSmith,
            NewPlus,
            Peek,
            MouseWithoutBorders,
            QuickAccent,
            CommandNotFound,
        }

        private static string SettingsWindowNameToString(SettingsWindow value)
        {
            switch (value)
            {
                case SettingsWindow.Overview:
                    return "Overview";
                case SettingsWindow.Awake:
                    return "Awake";
                case SettingsWindow.ColorPicker:
                    return "ColorPicker";
                case SettingsWindow.FancyZones:
                    return "FancyZones";
                case SettingsWindow.Run:
                    return "Run";
                case SettingsWindow.ImageResizer:
                    return "ImageResizer";
                case SettingsWindow.KBM:
                    return "KBM";
                case SettingsWindow.MouseUtils:
                    return "MouseUtils";
                case SettingsWindow.PowerRename:
                    return "PowerRename";
                case SettingsWindow.FileExplorer:
                    return "FileExplorer";
                case SettingsWindow.ShortcutGuide:
                    return "ShortcutGuide";
                case SettingsWindow.Hosts:
                    return "Hosts";
                case SettingsWindow.MeasureTool:
                    return "MeasureTool";
                case SettingsWindow.PowerOCR:
                    return "PowerOcr";
                case SettingsWindow.RegistryPreview:
                    return "RegistryPreview";
                case SettingsWindow.CropAndLock:
                    return "CropAndLock";
                case SettingsWindow.EnvironmentVariables:
                    return "EnvironmentVariables";
                case SettingsWindow.Dashboard:
                    return "Dashboard";
                case SettingsWindow.AdvancedPaste:
                    return "AdvancedPaste";
                case SettingsWindow.Workspaces:
                    return "Workspaces";
                case SettingsWindow.CmdPal:
                    return "CmdPal";
                case SettingsWindow.ZoomIt:
                    return "ZoomIt";
                case SettingsWindow.AlwaysOnTop:
                    return "AlwaysOnTop";
                case SettingsWindow.FileLockSmith:
                    return "FileLockSmith";
                case SettingsWindow.NewPlus:
                    return "NewPlus";
                case SettingsWindow.Peek:
                    return "Peek";
                default:
                    {
                        return string.Empty;
                    }
            }
        }

        public static void OpenSettings(SettingsWindow window)
        {
            try
            {
                var exePath = Path.Combine(
                    PowerToysPathResolver.GetPowerToysInstallPath(),
                    "PowerToys.exe");

                if (exePath == null || !File.Exists(exePath))
                {
                    Logger.LogError($"Failed to find powertoys exe path, {exePath}");
                    return;
                }

                var args = "--open-settings=" + SettingsWindowNameToString(window);

                Process.Start(new ProcessStartInfo
                {
                    FileName = exePath,
                    Arguments = args,
                    UseShellExecute = false,
                });
            }
            catch (Exception ex)
            {
                Logger.LogError(ex.Message);
            }
        }
    }
}
