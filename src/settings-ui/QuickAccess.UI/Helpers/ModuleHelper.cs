// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using global::PowerToys.GPOWrapper;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library;

namespace Microsoft.PowerToys.QuickAccess.Helpers;

internal static class ModuleHelper
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
            ModuleType.LightSwitch => generalSettingsConfig.Enabled.LightSwitch,
            ModuleType.EnvironmentVariables => generalSettingsConfig.Enabled.EnvironmentVariables,
            ModuleType.FancyZones => generalSettingsConfig.Enabled.FancyZones,
            ModuleType.FileLocksmith => generalSettingsConfig.Enabled.FileLocksmith,
            ModuleType.FindMyMouse => generalSettingsConfig.Enabled.FindMyMouse,
            ModuleType.Hosts => generalSettingsConfig.Enabled.Hosts,
            ModuleType.ImageResizer => generalSettingsConfig.Enabled.ImageResizer,
            ModuleType.KeyboardManager => generalSettingsConfig.Enabled.KeyboardManager,
            ModuleType.MouseHighlighter => generalSettingsConfig.Enabled.MouseHighlighter,
            ModuleType.MouseJump => generalSettingsConfig.Enabled.MouseJump,
            ModuleType.MousePointerCrosshairs => generalSettingsConfig.Enabled.MousePointerCrosshairs,
            ModuleType.MouseWithoutBorders => generalSettingsConfig.Enabled.MouseWithoutBorders,
            ModuleType.NewPlus => generalSettingsConfig.Enabled.NewPlus,
            ModuleType.Peek => generalSettingsConfig.Enabled.Peek,
            ModuleType.PowerRename => generalSettingsConfig.Enabled.PowerRename,
            ModuleType.PowerLauncher => generalSettingsConfig.Enabled.PowerLauncher,
            ModuleType.PowerAccent => generalSettingsConfig.Enabled.PowerAccent,
            ModuleType.Workspaces => generalSettingsConfig.Enabled.Workspaces,
            ModuleType.RegistryPreview => generalSettingsConfig.Enabled.RegistryPreview,
            ModuleType.MeasureTool => generalSettingsConfig.Enabled.MeasureTool,
            ModuleType.ShortcutGuide => generalSettingsConfig.Enabled.ShortcutGuide,
            ModuleType.PowerOCR => generalSettingsConfig.Enabled.PowerOcr,
            ModuleType.ZoomIt => generalSettingsConfig.Enabled.ZoomIt,
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
            case ModuleType.LightSwitch: generalSettingsConfig.Enabled.LightSwitch = isEnabled; break;
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
        return moduleType switch
        {
            ModuleType.AdvancedPaste => GPOWrapper.GetConfiguredAdvancedPasteEnabledValue(),
            ModuleType.AlwaysOnTop => GPOWrapper.GetConfiguredAlwaysOnTopEnabledValue(),
            ModuleType.Awake => GPOWrapper.GetConfiguredAwakeEnabledValue(),
            ModuleType.CmdPal => GPOWrapper.GetConfiguredCmdPalEnabledValue(),
            ModuleType.ColorPicker => GPOWrapper.GetConfiguredColorPickerEnabledValue(),
            ModuleType.CropAndLock => GPOWrapper.GetConfiguredCropAndLockEnabledValue(),
            ModuleType.CursorWrap => GPOWrapper.GetConfiguredCursorWrapEnabledValue(),
            ModuleType.EnvironmentVariables => GPOWrapper.GetConfiguredEnvironmentVariablesEnabledValue(),
            ModuleType.FancyZones => GPOWrapper.GetConfiguredFancyZonesEnabledValue(),
            ModuleType.FileLocksmith => GPOWrapper.GetConfiguredFileLocksmithEnabledValue(),
            ModuleType.FindMyMouse => GPOWrapper.GetConfiguredFindMyMouseEnabledValue(),
            ModuleType.Hosts => GPOWrapper.GetConfiguredHostsFileEditorEnabledValue(),
            ModuleType.ImageResizer => GPOWrapper.GetConfiguredImageResizerEnabledValue(),
            ModuleType.KeyboardManager => GPOWrapper.GetConfiguredKeyboardManagerEnabledValue(),
            ModuleType.MouseHighlighter => GPOWrapper.GetConfiguredMouseHighlighterEnabledValue(),
            ModuleType.MouseJump => GPOWrapper.GetConfiguredMouseJumpEnabledValue(),
            ModuleType.MousePointerCrosshairs => GPOWrapper.GetConfiguredMousePointerCrosshairsEnabledValue(),
            ModuleType.MouseWithoutBorders => GPOWrapper.GetConfiguredMouseWithoutBordersEnabledValue(),
            ModuleType.NewPlus => GPOWrapper.GetConfiguredNewPlusEnabledValue(),
            ModuleType.Peek => GPOWrapper.GetConfiguredPeekEnabledValue(),
            ModuleType.PowerRename => GPOWrapper.GetConfiguredPowerRenameEnabledValue(),
            ModuleType.PowerLauncher => GPOWrapper.GetConfiguredPowerLauncherEnabledValue(),
            ModuleType.PowerAccent => GPOWrapper.GetConfiguredQuickAccentEnabledValue(),
            ModuleType.Workspaces => GPOWrapper.GetConfiguredWorkspacesEnabledValue(),
            ModuleType.RegistryPreview => GPOWrapper.GetConfiguredRegistryPreviewEnabledValue(),
            ModuleType.MeasureTool => GPOWrapper.GetConfiguredScreenRulerEnabledValue(),
            ModuleType.ShortcutGuide => GPOWrapper.GetConfiguredShortcutGuideEnabledValue(),
            ModuleType.PowerOCR => GPOWrapper.GetConfiguredTextExtractorEnabledValue(),
            ModuleType.ZoomIt => GPOWrapper.GetConfiguredZoomItEnabledValue(),
            _ => GpoRuleConfigured.Unavailable,
        };
    }
}
