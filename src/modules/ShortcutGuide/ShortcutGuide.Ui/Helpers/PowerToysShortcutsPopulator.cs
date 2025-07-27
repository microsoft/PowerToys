// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using Microsoft.PowerToys.Settings.UI.Library;

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
                content = HotkeySettingsToYaml(advancedPasteProperties.AdvancedPasteUIShortcut, "Advanced Paste", content, "Open Advanced Paste window");
                content = HotkeySettingsToYaml(advancedPasteProperties.PasteAsPlainTextShortcut, "Advanced Paste", content, "Paste as plain text directly");
                content = HotkeySettingsToYaml(advancedPasteProperties.PasteAsMarkdownShortcut, "Advanced Paste", content, "Paste as markdown directly");
                content = HotkeySettingsToYaml(advancedPasteProperties.PasteAsJsonShortcut, "Advanced Paste", content, "Paste as JSON directly");
                if (advancedPasteProperties.AdditionalActions.ImageToText.IsShown)
                {
                    content = HotkeySettingsToYaml(advancedPasteProperties.AdditionalActions.ImageToText.Shortcut, "Advanced Paste", content, "Paste image to text");
                }

                if (advancedPasteProperties.AdditionalActions.PasteAsFile.IsShown)
                {
                    content = HotkeySettingsToYaml(advancedPasteProperties.AdditionalActions.PasteAsFile.PasteAsTxtFile.Shortcut, "Advanced Paste", content, "Paste as .txt file");
                    content = HotkeySettingsToYaml(advancedPasteProperties.AdditionalActions.PasteAsFile.PasteAsPngFile.Shortcut, "Advanced Paste", content, "Paste as .png file");
                    content = HotkeySettingsToYaml(advancedPasteProperties.AdditionalActions.PasteAsFile.PasteAsHtmlFile.Shortcut, "Advanced Paste", content, "Paste as .html file");
                }

                if (advancedPasteProperties.AdditionalActions.Transcode.IsShown)
                {
                    content = HotkeySettingsToYaml(advancedPasteProperties.AdditionalActions.Transcode.TranscodeToMp3.Shortcut, "Advanced Paste", content, "Transcode to .mp3");
                    content = HotkeySettingsToYaml(advancedPasteProperties.AdditionalActions.Transcode.TranscodeToMp4.Shortcut, "Advanced Paste", content, "Transcode to .mp4");
                }
            }

            if (enabledModules.AlwaysOnTop)
            {
                content = HotkeySettingsToYaml(SettingsRepository<AlwaysOnTopSettings>.GetInstance(settingsUtils).SettingsConfig.Properties.Hotkey, "Always On Top", content, "Pin a window");
            }

            if (enabledModules.ColorPicker)
            {
                content = HotkeySettingsToYaml(SettingsRepository<ColorPickerSettings>.GetInstance(settingsUtils).SettingsConfig.Properties.ActivationShortcut, "Color Picker", content, "Pick a color");
            }

            if (enabledModules.CmdPal)
            {
                content = HotkeySettingsToYaml(new CmdPalProperties().Hotkey, "Command Palette", content, "Open Command Palette");
            }

            if (enabledModules.CropAndLock)
            {
                CropAndLockProperties cropAndLockProperties = SettingsRepository<CropAndLockSettings>.GetInstance(settingsUtils).SettingsConfig.Properties;
                content = HotkeySettingsToYaml(cropAndLockProperties.ThumbnailHotkey, "Crop And Lock", content, "Thumbnail");
                content = HotkeySettingsToYaml(cropAndLockProperties.ReparentHotkey, "Crop And Lock", content, "Reparent");
            }

            if (enabledModules.FancyZones)
            {
                content = HotkeySettingsToYaml(SettingsRepository<FancyZonesSettings>.GetInstance(settingsUtils).SettingsConfig.Properties.FancyzonesEditorHotkey, "FancyZones", content, "Open editor");
            }

            if (enabledModules.MouseHighlighter)
            {
                content = HotkeySettingsToYaml(SettingsRepository<MouseHighlighterSettings>.GetInstance(settingsUtils).SettingsConfig.Properties.ActivationShortcut, "Mouse Highlight", content, "Highlight clicks");
            }

            if (enabledModules.MouseJump)
            {
                content = HotkeySettingsToYaml(SettingsRepository<MouseJumpSettings>.GetInstance(settingsUtils).SettingsConfig.Properties.ActivationShortcut, "Mouse Jump", content, "Quickly move the mouse pointer");
            }

            if (enabledModules.MousePointerCrosshairs)
            {
                content = HotkeySettingsToYaml(SettingsRepository<MousePointerCrosshairsSettings>.GetInstance(settingsUtils).SettingsConfig.Properties.ActivationShortcut, "Mouse Pointer Crosshairs", content, "Show crosshairs");
            }

            if (enabledModules.Peek)
            {
                content = HotkeySettingsToYaml(SettingsRepository<PeekSettings>.GetInstance(settingsUtils).SettingsConfig.Properties.ActivationShortcut, "Peek", content);
            }

            if (enabledModules.PowerLauncher)
            {
                content = HotkeySettingsToYaml(SettingsRepository<PowerLauncherSettings>.GetInstance(settingsUtils).SettingsConfig.Properties.OpenPowerLauncher, "PowerToys Run", content);
            }

            if (enabledModules.MeasureTool)
            {
                content = HotkeySettingsToYaml(SettingsRepository<MeasureToolSettings>.GetInstance(settingsUtils).SettingsConfig.Properties.ActivationShortcut, "Screen Ruler", content);
            }

            if (enabledModules.ShortcutGuide)
            {
                content = HotkeySettingsToYaml(SettingsRepository<ShortcutGuideSettings>.GetInstance(settingsUtils).SettingsConfig.Properties.DefaultOpenShortcutGuide, "Shortcut Guide", content);
            }

            if (enabledModules.PowerOcr)
            {
                content = HotkeySettingsToYaml(SettingsRepository<PowerOcrSettings>.GetInstance(settingsUtils).SettingsConfig.Properties.ActivationShortcut, "Text Extractor", content);
            }

            if (enabledModules.Workspaces)
            {
                content = HotkeySettingsToYaml(SettingsRepository<WorkspacesSettings>.GetInstance(settingsUtils).SettingsConfig.Properties.Hotkey, "Workspaces", content);
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
