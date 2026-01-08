// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using ManagedCommon;

namespace Microsoft.PowerToys.Settings.UI.Library.Helpers
{
    public static class ModuleHelper
    {
        public static string GetModuleLabelResourceName(ModuleType moduleType)
        {
            return moduleType switch
            {
                ModuleType.Workspaces => "Workspaces/ModuleTitle",
                ModuleType.PowerAccent => "QuickAccent/ModuleTitle",
                ModuleType.PowerOCR => "TextExtractor/ModuleTitle",
                ModuleType.FindMyMouse => "MouseUtils_FindMyMouse/Header",
                ModuleType.MouseHighlighter => "MouseUtils_MouseHighlighter/Header",
                ModuleType.MouseJump => "MouseUtils_MouseJump/Header",
                ModuleType.MousePointerCrosshairs => "MouseUtils_MousePointerCrosshairs/Header",
                ModuleType.CursorWrap => "MouseUtils_CursorWrap/Header",
                ModuleType.GeneralSettings => "QuickAccessTitle/Title",
                _ => $"{moduleType}/ModuleTitle",
            };
        }

        public static string GetModuleTypeFluentIconName(ModuleType moduleType)
        {
            return moduleType switch
            {
                ModuleType.AdvancedPaste => "ms-appx:///Assets/Settings/Icons/AdvancedPaste.png",
                ModuleType.Workspaces => "ms-appx:///Assets/Settings/Icons/Workspaces.png",
                ModuleType.PowerOCR => "ms-appx:///Assets/Settings/Icons/TextExtractor.png",
                ModuleType.PowerAccent => "ms-appx:///Assets/Settings/Icons/QuickAccent.png",
                ModuleType.MousePointerCrosshairs => "ms-appx:///Assets/Settings/Icons/MouseCrosshairs.png",
                ModuleType.MeasureTool => "ms-appx:///Assets/Settings/Icons/ScreenRuler.png",
                ModuleType.PowerLauncher => "ms-appx:///Assets/Settings/Icons/PowerToysRun.png",
                ModuleType.GeneralSettings => "ms-appx:///Assets/Settings/Icons/PowerToys.png",
                _ => $"ms-appx:///Assets/Settings/Icons/{moduleType}.png",
            };
        }

        public static bool GetIsModuleEnabled(GeneralSettings generalSettingsConfig, ModuleType moduleType)
        {
            return moduleType switch
            {
                ModuleType.AdvancedPaste => generalSettingsConfig.Enabled.AdvancedPaste,
                ModuleType.AlwaysOnTop => generalSettingsConfig.Enabled.AlwaysOnTop,
                ModuleType.Awake => generalSettingsConfig.Enabled.Awake,
                ModuleType.CmdPal => generalSettingsConfig.Enabled.CmdPal,
                ModuleType.ColorPicker => generalSettingsConfig.Enabled.ColorPicker,
                ModuleType.CropAndLock => generalSettingsConfig.Enabled.CropAndLock,
                ModuleType.CursorWrap => generalSettingsConfig.Enabled.CursorWrap,
                ModuleType.EnvironmentVariables => generalSettingsConfig.Enabled.EnvironmentVariables,
                ModuleType.FancyZones => generalSettingsConfig.Enabled.FancyZones,
                ModuleType.FileLocksmith => generalSettingsConfig.Enabled.FileLocksmith,
                ModuleType.FindMyMouse => generalSettingsConfig.Enabled.FindMyMouse,
                ModuleType.Hosts => generalSettingsConfig.Enabled.Hosts,
                ModuleType.ImageResizer => generalSettingsConfig.Enabled.ImageResizer,
                ModuleType.KeyboardManager => generalSettingsConfig.Enabled.KeyboardManager,
                ModuleType.LightSwitch => generalSettingsConfig.Enabled.LightSwitch,
                ModuleType.MouseHighlighter => generalSettingsConfig.Enabled.MouseHighlighter,
                ModuleType.MouseJump => generalSettingsConfig.Enabled.MouseJump,
                ModuleType.MousePointerCrosshairs => generalSettingsConfig.Enabled.MousePointerCrosshairs,
                ModuleType.MouseWithoutBorders => generalSettingsConfig.Enabled.MouseWithoutBorders,
                ModuleType.NewPlus => generalSettingsConfig.Enabled.NewPlus,
                ModuleType.Peek => generalSettingsConfig.Enabled.Peek,
                ModuleType.PowerRename => generalSettingsConfig.Enabled.PowerRename,
                ModuleType.PowerLauncher => generalSettingsConfig.Enabled.PowerLauncher,
                ModuleType.PowerAccent => generalSettingsConfig.Enabled.PowerAccent,
                ModuleType.RegistryPreview => generalSettingsConfig.Enabled.RegistryPreview,
                ModuleType.MeasureTool => generalSettingsConfig.Enabled.MeasureTool,
                ModuleType.ShortcutGuide => generalSettingsConfig.Enabled.ShortcutGuide,
                ModuleType.PowerOCR => generalSettingsConfig.Enabled.PowerOcr,
                ModuleType.PowerDisplay => generalSettingsConfig.Enabled.PowerDisplay,
                ModuleType.Workspaces => generalSettingsConfig.Enabled.Workspaces,
                ModuleType.ZoomIt => generalSettingsConfig.Enabled.ZoomIt,
                ModuleType.GeneralSettings => generalSettingsConfig.EnableQuickAccess,
                _ => false,
            };
        }

        public static void SetIsModuleEnabled(GeneralSettings generalSettingsConfig, ModuleType moduleType, bool isEnabled)
        {
            switch (moduleType)
            {
                case ModuleType.AdvancedPaste: generalSettingsConfig.Enabled.AdvancedPaste = isEnabled; break;
                case ModuleType.AlwaysOnTop: generalSettingsConfig.Enabled.AlwaysOnTop = isEnabled; break;
                case ModuleType.Awake: generalSettingsConfig.Enabled.Awake = isEnabled; break;
                case ModuleType.CmdPal: generalSettingsConfig.Enabled.CmdPal = isEnabled; break;
                case ModuleType.ColorPicker: generalSettingsConfig.Enabled.ColorPicker = isEnabled; break;
                case ModuleType.CropAndLock: generalSettingsConfig.Enabled.CropAndLock = isEnabled; break;
                case ModuleType.CursorWrap: generalSettingsConfig.Enabled.CursorWrap = isEnabled; break;
                case ModuleType.EnvironmentVariables: generalSettingsConfig.Enabled.EnvironmentVariables = isEnabled; break;
                case ModuleType.FancyZones: generalSettingsConfig.Enabled.FancyZones = isEnabled; break;
                case ModuleType.FileLocksmith: generalSettingsConfig.Enabled.FileLocksmith = isEnabled; break;
                case ModuleType.FindMyMouse: generalSettingsConfig.Enabled.FindMyMouse = isEnabled; break;
                case ModuleType.Hosts: generalSettingsConfig.Enabled.Hosts = isEnabled; break;
                case ModuleType.ImageResizer: generalSettingsConfig.Enabled.ImageResizer = isEnabled; break;
                case ModuleType.KeyboardManager: generalSettingsConfig.Enabled.KeyboardManager = isEnabled; break;
                case ModuleType.LightSwitch: generalSettingsConfig.Enabled.LightSwitch = isEnabled; break;
                case ModuleType.MouseHighlighter: generalSettingsConfig.Enabled.MouseHighlighter = isEnabled; break;
                case ModuleType.MouseJump: generalSettingsConfig.Enabled.MouseJump = isEnabled; break;
                case ModuleType.MousePointerCrosshairs: generalSettingsConfig.Enabled.MousePointerCrosshairs = isEnabled; break;
                case ModuleType.MouseWithoutBorders: generalSettingsConfig.Enabled.MouseWithoutBorders = isEnabled; break;
                case ModuleType.NewPlus: generalSettingsConfig.Enabled.NewPlus = isEnabled; break;
                case ModuleType.Peek: generalSettingsConfig.Enabled.Peek = isEnabled; break;
                case ModuleType.PowerRename: generalSettingsConfig.Enabled.PowerRename = isEnabled; break;
                case ModuleType.PowerLauncher: generalSettingsConfig.Enabled.PowerLauncher = isEnabled; break;
                case ModuleType.PowerAccent: generalSettingsConfig.Enabled.PowerAccent = isEnabled; break;
                case ModuleType.RegistryPreview: generalSettingsConfig.Enabled.RegistryPreview = isEnabled; break;
                case ModuleType.MeasureTool: generalSettingsConfig.Enabled.MeasureTool = isEnabled; break;
                case ModuleType.ShortcutGuide: generalSettingsConfig.Enabled.ShortcutGuide = isEnabled; break;
                case ModuleType.PowerOCR: generalSettingsConfig.Enabled.PowerOcr = isEnabled; break;
                case ModuleType.PowerDisplay: generalSettingsConfig.Enabled.PowerDisplay = isEnabled; break;
                case ModuleType.Workspaces: generalSettingsConfig.Enabled.Workspaces = isEnabled; break;
                case ModuleType.ZoomIt: generalSettingsConfig.Enabled.ZoomIt = isEnabled; break;
                case ModuleType.GeneralSettings: generalSettingsConfig.EnableQuickAccess = isEnabled; break;
            }
        }
    }
}
