// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using global::PowerToys.GPOWrapper;
using ManagedCommon;

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

        public static string GetModulmoduleTypeFluentIconName(ModuleType moduleType)
        {
            switch (moduleType)
            {
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
    }
}
