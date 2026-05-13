// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.PowerToys.Settings.UI.Library;
using static ShortcutGuide.Helpers.ResourceLoaderInstance;

namespace ShortcutGuide.Helpers
{
    /// <summary>
    /// Populates the PowerToys shortcuts in the manifest files.
    /// </summary>
    internal sealed partial class PowerToysShortcutsPopulator
    {
        /// <summary>
        /// Populates the PowerToys shortcuts in the manifest files.
        /// </summary>
        public static void Populate()
        {
            string path = Path.Combine(ManifestInterpreter.PathOfManifestFiles, $"Microsoft.PowerToys.{ManifestInterpreter.Language}.yml");

            StringBuilder content = new(File.ReadAllText(path));

            const string populateStartString = "# <Populate start>";
            const string populateEndString = "# <Populate end>";

            content = new(PopulateRegex().Replace(content.ToString(), populateStartString + Environment.NewLine));

            SettingsUtils settingsUtils = SettingsUtils.Default;
            EnabledModules enabledModules = SettingsRepository<GeneralSettings>.GetInstance(settingsUtils).SettingsConfig.Enabled;
            if (enabledModules.AdvancedPaste)
            {
                AdvancedPasteProperties advancedPasteProperties = SettingsRepository<AdvancedPasteSettings>.GetInstance(settingsUtils).SettingsConfig.Properties;
                content.Append(HotkeySettingsToYaml(advancedPasteProperties.AdvancedPasteUIShortcut, SettingsResourceLoader.GetString("AdvancedPaste/ModuleTitle"), SettingsResourceLoader.GetString("AdvancedPasteUI_Shortcut/Header")));
                content.Append(HotkeySettingsToYaml(advancedPasteProperties.PasteAsPlainTextShortcut, SettingsResourceLoader.GetString("AdvancedPaste/ModuleTitle"), SettingsResourceLoader.GetString("PasteAsPlainText_Shortcut/Header")));
                content.Append(HotkeySettingsToYaml(advancedPasteProperties.PasteAsMarkdownShortcut, SettingsResourceLoader.GetString("AdvancedPaste/ModuleTitle"), SettingsResourceLoader.GetString("PasteAsMarkdown_Shortcut/Header")));
                content.Append(HotkeySettingsToYaml(advancedPasteProperties.PasteAsJsonShortcut, SettingsResourceLoader.GetString("AdvancedPaste/ModuleTitle"), SettingsResourceLoader.GetString("PasteAsJson_Shortcut/Header")));
                if (advancedPasteProperties.AdditionalActions.ImageToText.IsShown)
                {
                    content.Append(HotkeySettingsToYaml(advancedPasteProperties.AdditionalActions.ImageToText.Shortcut, SettingsResourceLoader.GetString("AdvancedPaste/ModuleTitle"), SettingsResourceLoader.GetString("ImageToText/Header")));
                }

                if (advancedPasteProperties.AdditionalActions.PasteAsFile.IsShown)
                {
                    content.Append(HotkeySettingsToYaml(advancedPasteProperties.AdditionalActions.PasteAsFile.PasteAsTxtFile.Shortcut, SettingsResourceLoader.GetString("AdvancedPaste/ModuleTitle"), SettingsResourceLoader.GetString("PasteAsTxtFile/Header")));
                    content.Append(HotkeySettingsToYaml(advancedPasteProperties.AdditionalActions.PasteAsFile.PasteAsPngFile.Shortcut, SettingsResourceLoader.GetString("AdvancedPaste/ModuleTitle"), SettingsResourceLoader.GetString("PasteAsPngFile/Header")));
                    content.Append(HotkeySettingsToYaml(advancedPasteProperties.AdditionalActions.PasteAsFile.PasteAsHtmlFile.Shortcut, SettingsResourceLoader.GetString("AdvancedPaste/ModuleTitle"), SettingsResourceLoader.GetString("PasteAsHtmlFile/Header")));
                }

                if (advancedPasteProperties.AdditionalActions.Transcode.IsShown)
                {
                    content.Append(HotkeySettingsToYaml(advancedPasteProperties.AdditionalActions.Transcode.TranscodeToMp3.Shortcut, SettingsResourceLoader.GetString("AdvancedPaste/ModuleTitle"), SettingsResourceLoader.GetString("TranscodeToMp3/Header")));
                    content.Append(HotkeySettingsToYaml(advancedPasteProperties.AdditionalActions.Transcode.TranscodeToMp4.Shortcut, SettingsResourceLoader.GetString("AdvancedPaste/ModuleTitle"), SettingsResourceLoader.GetString("TranscodeToMp4/Header")));
                }
            }

            if (enabledModules.AlwaysOnTop)
            {
                content.Append(HotkeySettingsToYaml(SettingsRepository<AlwaysOnTopSettings>.GetInstance(settingsUtils).SettingsConfig.Properties.Hotkey, SettingsResourceLoader.GetString("AlwaysOnTop/ModuleTitle"), SettingsResourceLoader.GetString("AlwaysOnTop_ShortDescription")));
            }

            if (enabledModules.ColorPicker)
            {
                content.Append(HotkeySettingsToYaml(SettingsRepository<ColorPickerSettings>.GetInstance(settingsUtils).SettingsConfig.Properties.ActivationShortcut, SettingsResourceLoader.GetString("ColorPicker/ModuleTitle"), SettingsResourceLoader.GetString("ColorPicker_ShortDescription")));
            }

            if (enabledModules.CmdPal)
            {
                content.Append(HotkeySettingsToYaml(new CmdPalProperties().Hotkey, SettingsResourceLoader.GetString("CmdPal/ModuleTitle")));
            }

            if (enabledModules.CropAndLock)
            {
                CropAndLockProperties cropAndLockProperties = SettingsRepository<CropAndLockSettings>.GetInstance(settingsUtils).SettingsConfig.Properties;
                content.Append(HotkeySettingsToYaml(cropAndLockProperties.ThumbnailHotkey, SettingsResourceLoader.GetString("CropAndLock/ModuleTitle"), SettingsResourceLoader.GetString("CropAndLock_Thumbnail")));
                content.Append(HotkeySettingsToYaml(cropAndLockProperties.ReparentHotkey, SettingsResourceLoader.GetString("CropAndLock/ModuleTitle"), SettingsResourceLoader.GetString("CropAndLock_Reparent")));
            }

            if (enabledModules.CursorWrap)
            {
                content.Append(HotkeySettingsToYaml(SettingsRepository<CursorWrapSettings>.GetInstance(settingsUtils).SettingsConfig.Properties.ActivationShortcut, SettingsResourceLoader.GetString("MouseUtils_CursorWrap/Header"), SettingsResourceLoader.GetString("MouseUtils_CursorWrap/Description")));
            }

            if (enabledModules.FancyZones)
            {
                content.Append(HotkeySettingsToYaml(SettingsRepository<FancyZonesSettings>.GetInstance(settingsUtils).SettingsConfig.Properties.FancyzonesEditorHotkey, SettingsResourceLoader.GetString("FancyZones/ModuleTitle"), SettingsResourceLoader.GetString("FancyZones_OpenEditor")));
            }

            if (enabledModules.LightSwitch)
            {
                content.Append(HotkeySettingsToYaml(SettingsRepository<LightSwitchSettings>.GetInstance(settingsUtils).SettingsConfig.Properties.ToggleThemeHotkey, SettingsResourceLoader.GetString("LightSwitch/ModuleTitle"), SettingsResourceLoader.GetString("LightSwitch_ForceDarkMode")));
            }

            if (enabledModules.MouseHighlighter)
            {
                content.Append(HotkeySettingsToYaml(SettingsRepository<MouseHighlighterSettings>.GetInstance(settingsUtils).SettingsConfig.Properties.ActivationShortcut, SettingsResourceLoader.GetString("MouseUtils_MouseHighlighter/Header"), SettingsResourceLoader.GetString("MouseHighlighter_ShortDescription")));
            }

            if (enabledModules.MouseJump)
            {
                content.Append(HotkeySettingsToYaml(SettingsRepository<MouseJumpSettings>.GetInstance(settingsUtils).SettingsConfig.Properties.ActivationShortcut, SettingsResourceLoader.GetString("MouseUtils_MouseJump/Header"), SettingsResourceLoader.GetString("MouseJump_ShortDescription")));
            }

            if (enabledModules.MousePointerCrosshairs)
            {
                content.Append(HotkeySettingsToYaml(SettingsRepository<MousePointerCrosshairsSettings>.GetInstance(settingsUtils).SettingsConfig.Properties.ActivationShortcut, SettingsResourceLoader.GetString("MouseUtils_MousePointerCrosshairs/Header"), SettingsResourceLoader.GetString("MouseCrosshairs_ShortDescription")));
            }

            if (enabledModules.Peek)
            {
                content.Append(HotkeySettingsToYaml(SettingsRepository<PeekSettings>.GetInstance(settingsUtils).SettingsConfig.Properties.ActivationShortcut, SettingsResourceLoader.GetString("Peek/ModuleTitle")));
            }

            if (enabledModules.PowerLauncher)
            {
                content.Append(HotkeySettingsToYaml(SettingsRepository<PowerLauncherSettings>.GetInstance(settingsUtils).SettingsConfig.Properties.OpenPowerLauncher, SettingsResourceLoader.GetString("PowerLauncher/ModuleTitle")));
            }

            if (enabledModules.MeasureTool)
            {
                content.Append(HotkeySettingsToYaml(SettingsRepository<MeasureToolSettings>.GetInstance(settingsUtils).SettingsConfig.Properties.ActivationShortcut, SettingsResourceLoader.GetString("MeasureTool/ModuleTitle"), SettingsResourceLoader.GetString("ScreenRuler_ShortDescription")));
            }

            if (enabledModules.ShortcutGuide)
            {
                content.Append(HotkeySettingsToYaml(SettingsRepository<ShortcutGuideSettings>.GetInstance(settingsUtils).SettingsConfig.Properties.DefaultOpenShortcutGuide, SettingsResourceLoader.GetString("ShortcutGuide/ModuleTitle"), SettingsResourceLoader.GetString("ShortcutGuide_ShortDescription")));
            }

            if (enabledModules.PowerOcr)
            {
                content.Append(HotkeySettingsToYaml(SettingsRepository<PowerOcrSettings>.GetInstance(settingsUtils).SettingsConfig.Properties.ActivationShortcut, SettingsResourceLoader.GetString("TextExtractor/ModuleTitle"), SettingsResourceLoader.GetString("PowerOcr_ShortDescription")));
            }

            if (enabledModules.Workspaces)
            {
                content.Append(HotkeySettingsToYaml(SettingsRepository<WorkspacesSettings>.GetInstance(settingsUtils).SettingsConfig.Properties.Hotkey, SettingsResourceLoader.GetString("Workspaces/ModuleTitle"), SettingsResourceLoader.GetString("Workspaces_ShortDescription")));
            }

            // Todo: ZoomIt hotkeys currently not supported, because ZoomIt does save their settings in the view model instead of the settings properties, which is weird.
            /*
            if (enabledModules.ZoomIt)
            {
                content.Append(HotkeySettingsToYaml(SettingsRepository<ZoomItSettings>.GetInstance(settingsUtils).SettingsConfig.Properties.ToggleKey, SettingsResourceLoader.GetString("ZoomIt/ModuleTitle"), SettingsResourceLoader.GetString("ZoomIt_ZoomGroup/Header")));
                content.Append(HotkeySettingsToYaml(SettingsRepository<ZoomItSettings>.GetInstance(settingsUtils).SettingsConfig.Properties.LiveZoomToggleKey, SettingsResourceLoader.GetString("ZoomIt/ModuleTitle"), SettingsResourceLoader.GetString("ZoomIt_LiveZoomGroup/Header")));
                content.Append(HotkeySettingsToYaml(SettingsRepository<ZoomItSettings>.GetInstance(settingsUtils).SettingsConfig.Properties.DrawToggleKey, SettingsResourceLoader.GetString("ZoomIt/ModuleTitle"), SettingsResourceLoader.GetString("ZoomIt_DrawGroup/Header")));
                content.Append(HotkeySettingsToYaml(SettingsRepository<ZoomItSettings>.GetInstance(settingsUtils).SettingsConfig.Properties.DemoTypeToggleKey, SettingsResourceLoader.GetString("ZoomIt/ModuleTitle"), SettingsResourceLoader.GetString("ZoomIt_DemoTypeGroup/Header")));
                content.Append(HotkeySettingsToYaml(SettingsRepository<ZoomItSettings>.GetInstance(settingsUtils).SettingsConfig.Properties.BreakTimerKey, SettingsResourceLoader.GetString("ZoomIt/ModuleTitle"), SettingsResourceLoader.GetString("ZoomIt_BreakGroup/Header")));
                content.Append(HotkeySettingsToYaml(SettingsRepository<ZoomItSettings>.GetInstance(settingsUtils).SettingsConfig.Properties.RecordToggleKey, SettingsResourceLoader.GetString("ZoomIt/ModuleTitle"), SettingsResourceLoader.GetString("ZoomIt_RecordGroup/Header")));
                content.Append(HotkeySettingsToYaml(SettingsRepository<ZoomItSettings>.GetInstance(settingsUtils).SettingsConfig.Properties.SnipToggleKey, SettingsResourceLoader.GetString("ZoomIt/ModuleTitle"), SettingsResourceLoader.GetString("ZoomIt_SnipGroup/Header")));
            }*/

            content.Append(populateEndString);

            File.WriteAllText(path, content.ToString());
        }

        /// <summary>
        /// Converts the hotkey settings to a YAML format string for the manifest file.
        /// </summary>
        /// <param name="hotkeySettings">Object containing a hotkey from the settings.</param>
        /// <param name="moduleName">The name of the PowerToys module.</param>
        /// <param name="description">Description of the action.</param>
        /// <returns>Yaml code for the manifest file.</returns>
        private static string HotkeySettingsToYaml(HotkeySettings hotkeySettings, string moduleName, string? description = null)
        {
            string content = string.Empty;
            content += "      - Name: " + moduleName + Environment.NewLine;
            content += "        Shortcut: " + Environment.NewLine;
            content += "        - Win: " + hotkeySettings.Win.ToString() + Environment.NewLine;
            content += "          Ctrl: " + hotkeySettings.Ctrl.ToString() + Environment.NewLine;
            content += "          Alt: " + hotkeySettings.Alt.ToString() + Environment.NewLine;
            content += "          Shift: " + hotkeySettings.Shift.ToString() + Environment.NewLine;
            content += "          Keys:" + Environment.NewLine;
            content += "            - " + hotkeySettings.Code.ToString(CultureInfo.InvariantCulture) + Environment.NewLine;
            if (description != null)
            {
                content += "        Description: " + description + Environment.NewLine;
            }

            return content;
        }

        /// <inheritdoc cref="HotkeySettingsToYaml(HotkeySettings, string, string?)"/>
        private static string HotkeySettingsToYaml(KeyboardKeysProperty hotkeySettings, string moduleName, string? description = null)
        {
            return HotkeySettingsToYaml(hotkeySettings.Value, moduleName, description);
        }

        [GeneratedRegex(@"# <Populate start>[\s\S\n\r]*# <Populate end>")]
        private static partial Regex PopulateRegex();
    }
}
