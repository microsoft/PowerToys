// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using global::PowerToys.GPOWrapper;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library;

namespace Microsoft.PowerToys.QuickAccess.Helpers;

internal static class ModuleGpoHelper
{
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
