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
                case ModuleType.MousePointerCrosshairs: return "ms-appx:///Assets/Settings/FluentIcons/FluentIconsMouseCrosshairs.png";
                case ModuleType.MeasureTool: return "ms-appx:///Assets/Settings/FluentIcons/FluentIconsScreenRuler.png";
                case ModuleType.PowerLauncher: return $"ms-appx:///Assets/Settings/FluentIcons/FluentIconsPowerToysRun.png";
                default: return $"ms-appx:///Assets/Settings/FluentIcons/FluentIcons{moduleType}.png";
            }
        }

        public static bool GetIsModuleEnabled(Library.GeneralSettings generalSettingsConfig, ModuleType moduleType)
        {
            switch (moduleType)
            {
                case ModuleType.AlwaysOnTop: return generalSettingsConfig.Enabled.AlwaysOnTop;
                case ModuleType.Awake: return generalSettingsConfig.Enabled.Awake;
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
                case ModuleType.PastePlain: return generalSettingsConfig.Enabled.PastePlain;
                case ModuleType.Peek: return generalSettingsConfig.Enabled.Peek;
                case ModuleType.PowerRename: return generalSettingsConfig.Enabled.PowerRename;
                case ModuleType.PowerLauncher: return generalSettingsConfig.Enabled.PowerLauncher;
                case ModuleType.PowerAccent: return generalSettingsConfig.Enabled.PowerAccent;
                case ModuleType.RegistryPreview: return generalSettingsConfig.Enabled.RegistryPreview;
                case ModuleType.MeasureTool: return generalSettingsConfig.Enabled.MeasureTool;
                case ModuleType.ShortcutGuide: return generalSettingsConfig.Enabled.ShortcutGuide;
                case ModuleType.PowerOCR: return generalSettingsConfig.Enabled.PowerOCR;
                default: return false;
            }
        }

        internal static void SetIsModuleEnabled(GeneralSettings generalSettingsConfig, ModuleType moduleType, bool isEnabled)
        {
            switch (moduleType)
            {
                case ModuleType.AlwaysOnTop: generalSettingsConfig.Enabled.AlwaysOnTop = isEnabled; break;
                case ModuleType.Awake: generalSettingsConfig.Enabled.Awake = isEnabled; break;
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
                case ModuleType.PastePlain: generalSettingsConfig.Enabled.PastePlain = isEnabled; break;
                case ModuleType.Peek: generalSettingsConfig.Enabled.Peek = isEnabled; break;
                case ModuleType.PowerRename: generalSettingsConfig.Enabled.PowerRename = isEnabled; break;
                case ModuleType.PowerLauncher: generalSettingsConfig.Enabled.PowerLauncher = isEnabled; break;
                case ModuleType.PowerAccent: generalSettingsConfig.Enabled.PowerAccent = isEnabled; break;
                case ModuleType.RegistryPreview: generalSettingsConfig.Enabled.RegistryPreview = isEnabled; break;
                case ModuleType.MeasureTool: generalSettingsConfig.Enabled.MeasureTool = isEnabled; break;
                case ModuleType.ShortcutGuide: generalSettingsConfig.Enabled.ShortcutGuide = isEnabled; break;
                case ModuleType.PowerOCR: generalSettingsConfig.Enabled.PowerOCR = isEnabled; break;
            }
        }

        public static GpoRuleConfigured GetModuleGpoConfiguration(ModuleType moduleType)
        {
            switch (moduleType)
            {
                case ModuleType.AlwaysOnTop: return GPOWrapper.GetConfiguredAlwaysOnTopEnabledValue();
                case ModuleType.Awake: return GPOWrapper.GetConfiguredAwakeEnabledValue();
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
                case ModuleType.PastePlain: return GPOWrapper.GetConfiguredPastePlainEnabledValue();
                case ModuleType.Peek: return GPOWrapper.GetConfiguredPeekEnabledValue();
                case ModuleType.PowerRename: return GPOWrapper.GetConfiguredPowerRenameEnabledValue();
                case ModuleType.PowerLauncher: return GPOWrapper.GetConfiguredPowerLauncherEnabledValue();
                case ModuleType.PowerAccent: return GPOWrapper.GetConfiguredQuickAccentEnabledValue();
                case ModuleType.RegistryPreview: return GPOWrapper.GetConfiguredRegistryPreviewEnabledValue();
                case ModuleType.MeasureTool: return GPOWrapper.GetConfiguredScreenRulerEnabledValue();
                case ModuleType.ShortcutGuide: return GPOWrapper.GetConfiguredShortcutGuideEnabledValue();
                case ModuleType.PowerOCR: return GPOWrapper.GetConfiguredTextExtractorEnabledValue();
                default: return GpoRuleConfigured.Unavailable;
            }
        }

        public static Color GetModuleAccentColor(ModuleType moduleType)
        {
            return moduleType switch
            {
                ModuleType.AlwaysOnTop => Color.FromArgb(255, 74, 196, 242), // #4ac4f2
                ModuleType.Awake => Color.FromArgb(255, 40, 177, 233), // #28b1e9
                ModuleType.ColorPicker => Color.FromArgb(255, 7, 129, 211), // #0781d3
                ModuleType.CropAndLock => Color.FromArgb(255, 32, 166, 228), // #20a6e4
                ModuleType.EnvironmentVariables => Color.FromArgb(255, 16, 132, 208), // #1084d0
                ModuleType.FancyZones => Color.FromArgb(255, 65, 209, 247), // #41d1f7
                ModuleType.FileLocksmith => Color.FromArgb(255, 245, 161, 20), // #f5a114
                ModuleType.FindMyMouse => Color.FromArgb(255, 104, 109, 112), // #686d70
                ModuleType.Hosts => Color.FromArgb(255, 16, 132, 208), // #1084d0
                ModuleType.ImageResizer => Color.FromArgb(255, 85, 207, 248), // #55cff8
                ModuleType.KeyboardManager => Color.FromArgb(255, 224, 231, 238), // #e0e7ee
                ModuleType.MouseHighlighter => Color.FromArgb(255, 17, 126, 199), // #117ec7
                ModuleType.MouseJump => Color.FromArgb(255, 240, 240, 239), // #f0f0ef
                ModuleType.MousePointerCrosshairs => Color.FromArgb(255, 25, 115, 182), // #1973b6
                ModuleType.MouseWithoutBorders => Color.FromArgb(255, 31, 164, 227), // #1fa4e3
                ModuleType.PastePlain => Color.FromArgb(255, 243, 156, 16), // #f39c10
                ModuleType.Peek => Color.FromArgb(255, 255, 214, 103), // #ffd667
                ModuleType.PowerRename => Color.FromArgb(255, 43, 186, 243), // #2bbaf3
                ModuleType.PowerLauncher => Color.FromArgb(255, 51, 191, 240), // #33bff0
                ModuleType.PowerAccent => Color.FromArgb(255, 84, 89, 92), // #54595c
                ModuleType.RegistryPreview => Color.FromArgb(255, 17, 80, 138), // #11508a
                ModuleType.MeasureTool => Color.FromArgb(255, 135, 144, 153), // #879099
                ModuleType.ShortcutGuide => Color.FromArgb(255, 193, 202, 209), // #c1cad1
                ModuleType.PowerOCR => Color.FromArgb(255, 24, 153, 224), // #1899e0
                _ => Color.FromArgb(255, 255, 255, 255), // never called, all values listed above
            };
        }

        public static System.Type GetModulePageType(ModuleType moduleType)
        {
            return moduleType switch
            {
                ModuleType.AlwaysOnTop => typeof(AlwaysOnTopPage),
                ModuleType.Awake => typeof(AwakePage),
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
                ModuleType.PastePlain => typeof(PastePlainPage),
                ModuleType.Peek => typeof(PeekPage),
                ModuleType.PowerRename => typeof(PowerRenamePage),
                ModuleType.PowerLauncher => typeof(PowerLauncherPage),
                ModuleType.PowerAccent => typeof(PowerAccentPage),
                ModuleType.RegistryPreview => typeof(RegistryPreviewPage),
                ModuleType.MeasureTool => typeof(MeasureToolPage),
                ModuleType.ShortcutGuide => typeof(ShortcutGuidePage),
                ModuleType.PowerOCR => typeof(PowerOcrPage),
                _ => typeof(DashboardPage), // never called, all values listed above
            };
        }
    }
}
