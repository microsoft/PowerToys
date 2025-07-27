// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using Microsoft.PowerToys.Settings.UI.Library;
using static ShortcutGuide.Helpers.ResourceLoaderInstance;

namespace ShortcutGuide.Helpers
{
    internal sealed partial class PowerToysShortcutsPopulator
    {
        public static void Populate()
        {
            string path = Path.Combine(ManifestInterpreter.GetPathOfInterpretations(), $"Microsoft.PowerToys.{ManifestInterpreter.Language}.yml");

            string content = File.ReadAllText(path);

            const string populateStartString = "# <Populate start>";
            const string populateEndString = "# <Populate end>";

            content = PopulateRegex().Replace(content, populateStartString + Environment.NewLine);

            ISettingsUtils settingsUtils = new SettingsUtils();
            EnabledModules enabledModules = SettingsRepository<GeneralSettings>.GetInstance(settingsUtils).SettingsConfig.Enabled;
            if (enabledModules.AdvancedPaste)
            {
                AdvancedPasteProperties advancedPasteProperties = SettingsRepository<AdvancedPasteSettings>.GetInstance(settingsUtils).SettingsConfig.Properties;
                content = HotkeySettingsToYaml(advancedPasteProperties.AdvancedPasteUIShortcut, SettingsResourceLoader.GetString("AdvancedPaste/ModuleTitle"), content, SettingsResourceLoader.GetString("AdvancedPasteUI_Shortcut/Header"));
                content = HotkeySettingsToYaml(advancedPasteProperties.PasteAsPlainTextShortcut, SettingsResourceLoader.GetString("AdvancedPaste/ModuleTitle"), content, SettingsResourceLoader.GetString("PasteAsPlainText_Shortcut/Header"));
                content = HotkeySettingsToYaml(advancedPasteProperties.PasteAsMarkdownShortcut, SettingsResourceLoader.GetString("AdvancedPaste/ModuleTitle"), content, SettingsResourceLoader.GetString("PasteAsMarkdown_Shortcut/Header"));
                content = HotkeySettingsToYaml(advancedPasteProperties.PasteAsJsonShortcut, SettingsResourceLoader.GetString("AdvancedPaste/ModuleTitle"), content, SettingsResourceLoader.GetString("PasteAsJson_Shortcut/Header"));
                if (advancedPasteProperties.AdditionalActions.ImageToText.IsShown)
                {
                    content = HotkeySettingsToYaml(advancedPasteProperties.AdditionalActions.ImageToText.Shortcut, SettingsResourceLoader.GetString("AdvancedPaste/ModuleTitle"), content, SettingsResourceLoader.GetString("ImageToText/Header"));
                }

                if (advancedPasteProperties.AdditionalActions.PasteAsFile.IsShown)
                {
                    content = HotkeySettingsToYaml(advancedPasteProperties.AdditionalActions.PasteAsFile.PasteAsTxtFile.Shortcut, SettingsResourceLoader.GetString("AdvancedPaste/ModuleTitle"), content, SettingsResourceLoader.GetString("PasteAsTxtFile/Header"));
                    content = HotkeySettingsToYaml(advancedPasteProperties.AdditionalActions.PasteAsFile.PasteAsPngFile.Shortcut, SettingsResourceLoader.GetString("AdvancedPaste/ModuleTitle"), content, SettingsResourceLoader.GetString("PasteAsPngFile/Header"));
                    content = HotkeySettingsToYaml(advancedPasteProperties.AdditionalActions.PasteAsFile.PasteAsHtmlFile.Shortcut, SettingsResourceLoader.GetString("AdvancedPaste/ModuleTitle"), content, SettingsResourceLoader.GetString("PasteAsHtmlFile/Header"));
                }

                if (advancedPasteProperties.AdditionalActions.Transcode.IsShown)
                {
                    content = HotkeySettingsToYaml(advancedPasteProperties.AdditionalActions.Transcode.TranscodeToMp3.Shortcut, SettingsResourceLoader.GetString("AdvancedPaste/ModuleTitle"), content, SettingsResourceLoader.GetString("TranscodeToMp3/Header"));
                    content = HotkeySettingsToYaml(advancedPasteProperties.AdditionalActions.Transcode.TranscodeToMp4.Shortcut, SettingsResourceLoader.GetString("AdvancedPaste/ModuleTitle"), content, SettingsResourceLoader.GetString("TranscodeToMp4/Header"));
                }
            }

            if (enabledModules.AlwaysOnTop)
            {
                content = HotkeySettingsToYaml(SettingsRepository<AlwaysOnTopSettings>.GetInstance(settingsUtils).SettingsConfig.Properties.Hotkey, SettingsResourceLoader.GetString("AlwaysOnTop/ModuleTitle"), content, SettingsResourceLoader.GetString("AlwaysOnTop_ShortDescription"));
            }

            if (enabledModules.ColorPicker)
            {
                content = HotkeySettingsToYaml(SettingsRepository<ColorPickerSettings>.GetInstance(settingsUtils).SettingsConfig.Properties.ActivationShortcut, SettingsResourceLoader.GetString("ColorPicker/ModuleTitle"), content, SettingsResourceLoader.GetString("ColorPicker_ShortDescription"));
            }

            if (enabledModules.CmdPal)
            {
                content = HotkeySettingsToYaml(new CmdPalProperties().Hotkey, SettingsResourceLoader.GetString("CmdPal/ModuleTitle"), content);
            }

            if (enabledModules.CropAndLock)
            {
                CropAndLockProperties cropAndLockProperties = SettingsRepository<CropAndLockSettings>.GetInstance(settingsUtils).SettingsConfig.Properties;
                content = HotkeySettingsToYaml(cropAndLockProperties.ThumbnailHotkey, SettingsResourceLoader.GetString("CropAndLock/ModuleTitle"), content, SettingsResourceLoader.GetString("CropAndLock_Thumbnail"));
                content = HotkeySettingsToYaml(cropAndLockProperties.ReparentHotkey, SettingsResourceLoader.GetString("CropAndLock/ModuleTitle"), content, SettingsResourceLoader.GetString("CropAndLock_Reparent"));
            }

            if (enabledModules.FancyZones)
            {
                content = HotkeySettingsToYaml(SettingsRepository<FancyZonesSettings>.GetInstance(settingsUtils).SettingsConfig.Properties.FancyzonesEditorHotkey, SettingsResourceLoader.GetString("FancyZones/ModuleTitle"), content, SettingsResourceLoader.GetString("FancyZones_OpenEditor"));
            }

            if (enabledModules.MouseHighlighter)
            {
                content = HotkeySettingsToYaml(SettingsRepository<MouseHighlighterSettings>.GetInstance(settingsUtils).SettingsConfig.Properties.ActivationShortcut, SettingsResourceLoader.GetString("MouseUtils_MouseHighlighter/Header"), content, SettingsResourceLoader.GetString("MouseHighlighter_ShortDescription"));
            }

            if (enabledModules.MouseJump)
            {
                content = HotkeySettingsToYaml(SettingsRepository<MouseJumpSettings>.GetInstance(settingsUtils).SettingsConfig.Properties.ActivationShortcut, SettingsResourceLoader.GetString("MouseUtils_MouseJump/Header"), content, SettingsResourceLoader.GetString("MouseJump_ShortDescription"));
            }

            if (enabledModules.MousePointerCrosshairs)
            {
                content = HotkeySettingsToYaml(SettingsRepository<MousePointerCrosshairsSettings>.GetInstance(settingsUtils).SettingsConfig.Properties.ActivationShortcut, SettingsResourceLoader.GetString("MouseUtils_MousePointerCrosshairs/Header"), content, SettingsResourceLoader.GetString("MouseCrosshairs_ShortDescription"));
            }

            if (enabledModules.Peek)
            {
                content = HotkeySettingsToYaml(SettingsRepository<PeekSettings>.GetInstance(settingsUtils).SettingsConfig.Properties.ActivationShortcut, SettingsResourceLoader.GetString("Peek/ModuleTitle"), content);
            }

            if (enabledModules.PowerLauncher)
            {
                content = HotkeySettingsToYaml(SettingsRepository<PowerLauncherSettings>.GetInstance(settingsUtils).SettingsConfig.Properties.OpenPowerLauncher, SettingsResourceLoader.GetString("PowerLauncher/ModuleTitle"), content);
            }

            if (enabledModules.MeasureTool)
            {
                content = HotkeySettingsToYaml(SettingsRepository<MeasureToolSettings>.GetInstance(settingsUtils).SettingsConfig.Properties.ActivationShortcut, SettingsResourceLoader.GetString("MeasureTool/ModuleTitle"), content, SettingsResourceLoader.GetString("ScreenRuler_ShortDescription"));
            }

            if (enabledModules.ShortcutGuide)
            {
                content = HotkeySettingsToYaml(SettingsRepository<ShortcutGuideSettings>.GetInstance(settingsUtils).SettingsConfig.Properties.DefaultOpenShortcutGuide, SettingsResourceLoader.GetString("ShortcutGuide/ModuleTitle"), content, SettingsResourceLoader.GetString("ShortcutGuide_ShortDescription"));
            }

            if (enabledModules.PowerOcr)
            {
                content = HotkeySettingsToYaml(SettingsRepository<PowerOcrSettings>.GetInstance(settingsUtils).SettingsConfig.Properties.ActivationShortcut, SettingsResourceLoader.GetString("TextExtractor/ModuleTitle"), content, SettingsResourceLoader.GetString("PowerOcr_ShortDescription"));
            }

            if (enabledModules.Workspaces)
            {
                content = HotkeySettingsToYaml(SettingsRepository<WorkspacesSettings>.GetInstance(settingsUtils).SettingsConfig.Properties.Hotkey, SettingsResourceLoader.GetString("Workspaces/ModuleTitle"), content, SettingsResourceLoader.GetString("Workspaces_ShortDescription"));
            }

            content += populateEndString;

            File.WriteAllText(path, content);
        }

        public static string HotkeySettingsToYaml(HotkeySettings hotkeySettings, string moduleName, string content, string? description = null)
        {
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

        public static string HotkeySettingsToYaml(KeyboardKeysProperty keyboardKeys, string moduleName, string content, string? description = null)
        {
            return HotkeySettingsToYaml(keyboardKeys.Value, moduleName, content, description);
        }

        [GeneratedRegex(@"# <Populate start>[\s\S\n\r]*# <Populate end>")]
        private static partial Regex PopulateRegex();
    }
}
