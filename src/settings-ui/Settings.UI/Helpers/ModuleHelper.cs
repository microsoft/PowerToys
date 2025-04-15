// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using global::PowerToys.GPOWrapper;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Views;
using Windows.UI;

namespace Microsoft.PowerToys.Settings.UI.Helpers
{
    internal sealed class ModuleHelper
    {
        public static string GetModuleLabelResourceName(ModuleType moduleType)
        {
            switch (moduleType)
            {
                case ModuleType.Workspaces: return "Workspaces/ModuleTitle";
                case ModuleType.PowerAccent: return "QuickAccent/ModuleTitle";
                case ModuleType.PowerOCR: return "TextExtractor/ModuleTitle";
                case ModuleType.FindMyMouse:
                case ModuleType.MouseHighlighter:
                case ModuleType.MouseJump:
                case ModuleType.MousePointerCrosshairs: return $"MouseUtils_{moduleType}/Header";
                default: return $"{moduleType}/ModuleTitle";
            }
        }

        public static string GetModuleTypeFluentIconName(ModuleType moduleType)
        {
            switch (moduleType)
            {
                case ModuleType.AdvancedPaste: return "ms-appx:///Assets/Settings/Icons/AdvancedPaste.png";
                case ModuleType.Workspaces: return "ms-appx:///Assets/Settings/Icons/Workspaces.png";
                case ModuleType.PowerOCR: return "ms-appx:///Assets/Settings/Icons/TextExtractor.png";
                case ModuleType.PowerAccent: return "ms-appx:///Assets/Settings/Icons/QuickAccent.png";
                case ModuleType.MousePointerCrosshairs: return "ms-appx:///Assets/Settings/Icons/MouseCrosshairs.png";
                case ModuleType.MeasureTool: return "ms-appx:///Assets/Settings/Icons/ScreenRuler.png";
                case ModuleType.PowerLauncher: return $"ms-appx:///Assets/Settings/Icons/PowerToysRun.png";
                default: return $"ms-appx:///Assets/Settings/Icons/{moduleType}.png";
            }
        }

        public static bool GetIsModuleEnabled(Library.GeneralSettings generalSettingsConfig, ModuleType moduleType)
        {
            switch (moduleType)
            {
                case ModuleType.AdvancedPaste: return generalSettingsConfig.Enabled.AdvancedPaste;
                case ModuleType.AlwaysOnTop: return generalSettingsConfig.Enabled.AlwaysOnTop;
                case ModuleType.Awake: return generalSettingsConfig.Enabled.Awake;
                case ModuleType.CmdPal: return generalSettingsConfig.Enabled.CmdPal;
                case ModuleType.ColorPicker: return generalSettingsConfig.Enabled.ColorPicker;
                case ModuleType.CropAndLock: return generalSettingsConfig.Enabled.CropAndLock;
                case ModuleType.EnvironmentVariables: return generalSettingsConfig.Enabled.EnvironmentVariables;
                case ModuleType.FancyZones: return generalSettingsConfig.Enabled.FancyZones;
                case ModuleType.FileLocksmith: return generalSettingsConfig.Enabled.FileLocksmith;
                case ModuleType.FindMyMouse: return generalSettingsConfig.Enabled.FindMyMouse;
                case ModuleType.Hosts: return generalSettingsConfig.Enabled.Hosts;
                case ModuleType.ImageResizer: return generalSettingsConfig.Enabled.ImageResizer;
                case ModuleType.KeyboardManager: return generalSettingsConfig.Enabled.KeyboardManager;
                case ModuleType.MouseHighlighter: return generalSettingsConfig.Enabled.MouseHighlighter;
                case ModuleType.MouseJump: return generalSettingsConfig.Enabled.MouseJump;
                case ModuleType.MousePointerCrosshairs: return generalSettingsConfig.Enabled.MousePointerCrosshairs;
                case ModuleType.MouseWithoutBorders: return generalSettingsConfig.Enabled.MouseWithoutBorders;
                case ModuleType.NewPlus: return generalSettingsConfig.Enabled.NewPlus;
                case ModuleType.Peek: return generalSettingsConfig.Enabled.Peek;
                case ModuleType.PowerRename: return generalSettingsConfig.Enabled.PowerRename;
                case ModuleType.PowerLauncher: return generalSettingsConfig.Enabled.PowerLauncher;
                case ModuleType.PowerAccent: return generalSettingsConfig.Enabled.PowerAccent;
                case ModuleType.Workspaces: return generalSettingsConfig.Enabled.Workspaces;
                case ModuleType.RegistryPreview: return generalSettingsConfig.Enabled.RegistryPreview;
                case ModuleType.MeasureTool: return generalSettingsConfig.Enabled.MeasureTool;
                case ModuleType.ShortcutGuide: return generalSettingsConfig.Enabled.ShortcutGuide;
                case ModuleType.PowerOCR: return generalSettingsConfig.Enabled.PowerOcr;
                case ModuleType.ZoomIt: return generalSettingsConfig.Enabled.ZoomIt;
                default: return false;
            }
        }

        internal static void SetIsModuleEnabled(GeneralSettings generalSettingsConfig, ModuleType moduleType, bool isEnabled)
        {
            switch (moduleType)
            {
                case ModuleType.AdvancedPaste: generalSettingsConfig.Enabled.AdvancedPaste = isEnabled; break;
                case ModuleType.AlwaysOnTop: generalSettingsConfig.Enabled.AlwaysOnTop = isEnabled; break;
                case ModuleType.Awake: generalSettingsConfig.Enabled.Awake = isEnabled; break;
                case ModuleType.CmdPal: generalSettingsConfig.Enabled.CmdPal = isEnabled; break;
                case ModuleType.ColorPicker: generalSettingsConfig.Enabled.ColorPicker = isEnabled; break;
                case ModuleType.CropAndLock: generalSettingsConfig.Enabled.CropAndLock = isEnabled; break;
                case ModuleType.EnvironmentVariables: generalSettingsConfig.Enabled.EnvironmentVariables = isEnabled; break;
                case ModuleType.FancyZones: generalSettingsConfig.Enabled.FancyZones = isEnabled; break;
                case ModuleType.FileLocksmith: generalSettingsConfig.Enabled.FileLocksmith = isEnabled; break;
                case ModuleType.FindMyMouse: generalSettingsConfig.Enabled.FindMyMouse = isEnabled; break;
                case ModuleType.Hosts: generalSettingsConfig.Enabled.Hosts = isEnabled; break;
                case ModuleType.ImageResizer: generalSettingsConfig.Enabled.ImageResizer = isEnabled; break;
                case ModuleType.KeyboardManager: generalSettingsConfig.Enabled.KeyboardManager = isEnabled; break;
                case ModuleType.MouseHighlighter: generalSettingsConfig.Enabled.MouseHighlighter = isEnabled; break;
                case ModuleType.MouseJump: generalSettingsConfig.Enabled.MouseJump = isEnabled; break;
                case ModuleType.MousePointerCrosshairs: generalSettingsConfig.Enabled.MousePointerCrosshairs = isEnabled; break;
                case ModuleType.MouseWithoutBorders: generalSettingsConfig.Enabled.MouseWithoutBorders = isEnabled; break;
                case ModuleType.NewPlus: generalSettingsConfig.Enabled.NewPlus = isEnabled; break;
                case ModuleType.Peek: generalSettingsConfig.Enabled.Peek = isEnabled; break;
                case ModuleType.PowerRename: generalSettingsConfig.Enabled.PowerRename = isEnabled; break;
                case ModuleType.PowerLauncher: generalSettingsConfig.Enabled.PowerLauncher = isEnabled; break;
                case ModuleType.PowerAccent: generalSettingsConfig.Enabled.PowerAccent = isEnabled; break;
                case ModuleType.Workspaces: generalSettingsConfig.Enabled.Workspaces = isEnabled; break;
                case ModuleType.RegistryPreview: generalSettingsConfig.Enabled.RegistryPreview = isEnabled; break;
                case ModuleType.MeasureTool: generalSettingsConfig.Enabled.MeasureTool = isEnabled; break;
                case ModuleType.ShortcutGuide: generalSettingsConfig.Enabled.ShortcutGuide = isEnabled; break;
                case ModuleType.PowerOCR: generalSettingsConfig.Enabled.PowerOcr = isEnabled; break;
                case ModuleType.ZoomIt: generalSettingsConfig.Enabled.ZoomIt = isEnabled; break;
            }
        }

        public static GpoRuleConfigured GetModuleGpoConfiguration(ModuleType moduleType)
        {
            switch (moduleType)
            {
                case ModuleType.AdvancedPaste: return GPOWrapper.GetConfiguredAdvancedPasteEnabledValue();
                case ModuleType.AlwaysOnTop: return GPOWrapper.GetConfiguredAlwaysOnTopEnabledValue();
                case ModuleType.Awake: return GPOWrapper.GetConfiguredAwakeEnabledValue();
                case ModuleType.CmdPal: return GPOWrapper.GetConfiguredCmdPalEnabledValue();
                case ModuleType.ColorPicker: return GPOWrapper.GetConfiguredColorPickerEnabledValue();
                case ModuleType.CropAndLock: return GPOWrapper.GetConfiguredCropAndLockEnabledValue();
                case ModuleType.EnvironmentVariables: return GPOWrapper.GetConfiguredEnvironmentVariablesEnabledValue();
                case ModuleType.FancyZones: return GPOWrapper.GetConfiguredFancyZonesEnabledValue();
                case ModuleType.FileLocksmith: return GPOWrapper.GetConfiguredFileLocksmithEnabledValue();
                case ModuleType.FindMyMouse: return GPOWrapper.GetConfiguredFindMyMouseEnabledValue();
                case ModuleType.Hosts: return GPOWrapper.GetConfiguredHostsFileEditorEnabledValue();
                case ModuleType.ImageResizer: return GPOWrapper.GetConfiguredImageResizerEnabledValue();
                case ModuleType.KeyboardManager: return GPOWrapper.GetConfiguredKeyboardManagerEnabledValue();
                case ModuleType.MouseHighlighter: return GPOWrapper.GetConfiguredMouseHighlighterEnabledValue();
                case ModuleType.MouseJump: return GPOWrapper.GetConfiguredMouseJumpEnabledValue();
                case ModuleType.MousePointerCrosshairs: return GPOWrapper.GetConfiguredMousePointerCrosshairsEnabledValue();
                case ModuleType.MouseWithoutBorders: return GPOWrapper.GetConfiguredMouseWithoutBordersEnabledValue();
                case ModuleType.NewPlus: return GPOWrapper.GetConfiguredNewPlusEnabledValue();
                case ModuleType.Peek: return GPOWrapper.GetConfiguredPeekEnabledValue();
                case ModuleType.PowerRename: return GPOWrapper.GetConfiguredPowerRenameEnabledValue();
                case ModuleType.PowerLauncher: return GPOWrapper.GetConfiguredPowerLauncherEnabledValue();
                case ModuleType.PowerAccent: return GPOWrapper.GetConfiguredQuickAccentEnabledValue();
                case ModuleType.Workspaces: return GPOWrapper.GetConfiguredWorkspacesEnabledValue();
                case ModuleType.RegistryPreview: return GPOWrapper.GetConfiguredRegistryPreviewEnabledValue();
                case ModuleType.MeasureTool: return GPOWrapper.GetConfiguredScreenRulerEnabledValue();
                case ModuleType.ShortcutGuide: return GPOWrapper.GetConfiguredShortcutGuideEnabledValue();
                case ModuleType.PowerOCR: return GPOWrapper.GetConfiguredTextExtractorEnabledValue();
                case ModuleType.ZoomIt: return GPOWrapper.GetConfiguredZoomItEnabledValue();
                default: return GpoRuleConfigured.Unavailable;
            }
        }

        public static System.Type GetModulePageType(ModuleType moduleType)
        {
            return moduleType switch
            {
                ModuleType.AdvancedPaste => typeof(AdvancedPastePage),
                ModuleType.AlwaysOnTop => typeof(AlwaysOnTopPage),
                ModuleType.Awake => typeof(AwakePage),
                ModuleType.CmdPal => typeof(CmdPalPage),
                ModuleType.ColorPicker => typeof(ColorPickerPage),
                ModuleType.CropAndLock => typeof(CropAndLockPage),
                ModuleType.EnvironmentVariables => typeof(EnvironmentVariablesPage),
                ModuleType.FancyZones => typeof(FancyZonesPage),
                ModuleType.FileLocksmith => typeof(FileLocksmithPage),
                ModuleType.FindMyMouse => typeof(MouseUtilsPage),
                ModuleType.Hosts => typeof(HostsPage),
                ModuleType.ImageResizer => typeof(ImageResizerPage),
                ModuleType.KeyboardManager => typeof(KeyboardManagerPage),
                ModuleType.MouseHighlighter => typeof(MouseUtilsPage),
                ModuleType.MouseJump => typeof(MouseUtilsPage),
                ModuleType.MousePointerCrosshairs => typeof(MouseUtilsPage),
                ModuleType.MouseWithoutBorders => typeof(MouseWithoutBordersPage),
                ModuleType.NewPlus => typeof(NewPlusPage),
                ModuleType.Peek => typeof(PeekPage),
                ModuleType.PowerRename => typeof(PowerRenamePage),
                ModuleType.PowerLauncher => typeof(PowerLauncherPage),
                ModuleType.PowerAccent => typeof(PowerAccentPage),
                ModuleType.Workspaces => typeof(WorkspacesPage),
                ModuleType.RegistryPreview => typeof(RegistryPreviewPage),
                ModuleType.MeasureTool => typeof(MeasureToolPage),
                ModuleType.ShortcutGuide => typeof(ShortcutGuidePage),
                ModuleType.PowerOCR => typeof(PowerOcrPage),
                ModuleType.ZoomIt => typeof(ZoomItPage),
                _ => typeof(DashboardPage), // never called, all values listed above
            };
        }
    }
}
