// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using Microsoft.PowerToys.Settings.UI.Library;

namespace ShortcutGuide
{
    internal sealed class PowerToysShortcutsPopulator
    {
        public static void Populate()
        {
            string path = Path.Combine(YmlInterpreter.GetPathOfIntepretations(), "Microsoft.PowerToys.yml");

            string content = File.ReadAllText(path);

            const string populateStartString = "# <Populate start>";
            const string populateEndString = "# <Populate end>";

            content = Regex.Replace(content, populateStartString + "[\\s\\S\\n\\r]*" + populateEndString, populateStartString + Environment.NewLine);

            content = HotkeySettingsToYaml(SettingsRepository<AdvancedPasteSettings>.GetInstance(new SettingsUtils()).SettingsConfig.Properties.AdvancedPasteUIShortcut, "Advanced Paste", content, "Open Advanced Paste window");
            content = HotkeySettingsToYaml(SettingsRepository<AdvancedPasteSettings>.GetInstance(new SettingsUtils()).SettingsConfig.Properties.PasteAsPlainTextShortcut, "Advanced Paste", content, "Paste as plain text directly");
            content = HotkeySettingsToYaml(SettingsRepository<AdvancedPasteSettings>.GetInstance(new SettingsUtils()).SettingsConfig.Properties.PasteAsMarkdownShortcut, "Advanced Paste", content, "Paste as markdown directly");
            content = HotkeySettingsToYaml(SettingsRepository<AdvancedPasteSettings>.GetInstance(new SettingsUtils()).SettingsConfig.Properties.PasteAsJsonShortcut, "Advanced Paste", content, "Paste as JSON directly");
            content = HotkeySettingsToYaml(SettingsRepository<AlwaysOnTopSettings>.GetInstance(new SettingsUtils()).SettingsConfig.Properties.Hotkey, "Always On Top", content, "Pin a window");
            content = HotkeySettingsToYaml(SettingsRepository<ColorPickerSettings>.GetInstance(new SettingsUtils()).SettingsConfig.Properties.ActivationShortcut, "Color Picker", content, "Pick a color");
            content = HotkeySettingsToYaml(SettingsRepository<CropAndLockSettings>.GetInstance(new SettingsUtils()).SettingsConfig.Properties.ThumbnailHotkey, "Crop And Lock", content, "Thumbnail");
            content = HotkeySettingsToYaml(SettingsRepository<CropAndLockSettings>.GetInstance(new SettingsUtils()).SettingsConfig.Properties.ReparentHotkey, "Crop And Lock", content, "Reparent");
            content = HotkeySettingsToYaml(SettingsRepository<FancyZonesSettings>.GetInstance(new SettingsUtils()).SettingsConfig.Properties.FancyzonesEditorHotkey, "FancyZones", content, "Open editor");
            content = HotkeySettingsToYaml(SettingsRepository<MouseHighlighterSettings>.GetInstance(new SettingsUtils()).SettingsConfig.Properties.ActivationShortcut, "Mouse Highlight", content, "Highlight clicks");
            content = HotkeySettingsToYaml(SettingsRepository<MouseJumpSettings>.GetInstance(new SettingsUtils()).SettingsConfig.Properties.ActivationShortcut, "Mouse Jump", content, "Quickly move the mouse pointer");
            content = HotkeySettingsToYaml(SettingsRepository<MousePointerCrosshairsSettings>.GetInstance(new SettingsUtils()).SettingsConfig.Properties.ActivationShortcut, "Mouse Pointer Crosshairs", content, "Show crosshairs");
            content = HotkeySettingsToYaml(SettingsRepository<PeekSettings>.GetInstance(new SettingsUtils()).SettingsConfig.Properties.ActivationShortcut, "Peek", content);
            content = HotkeySettingsToYaml(SettingsRepository<PowerLauncherSettings>.GetInstance(new SettingsUtils()).SettingsConfig.Properties.OpenPowerLauncher, "PowerToys Run", content);
            content = HotkeySettingsToYaml(SettingsRepository<MeasureToolSettings>.GetInstance(new SettingsUtils()).SettingsConfig.Properties.ActivationShortcut, "Screen Ruler", content);
            {
                ShortcutGuideProperties settingsProperties = SettingsRepository<ShortcutGuideSettings>.GetInstance(new SettingsUtils()).SettingsConfig.Properties;
                content = settingsProperties.UseLegacyPressWinKeyBehavior.Value
                    ? HotkeySettingsToYaml(new HotkeySettings(true, false, false, false, 0), "Shortcut Guide", content)
                    : HotkeySettingsToYaml(settingsProperties.DefaultOpenShortcutGuide, "Shortcut Guide", content);
            }

            content = HotkeySettingsToYaml(SettingsRepository<PowerOcrSettings>.GetInstance(new SettingsUtils()).SettingsConfig.Properties.ActivationShortcut, "Text Extractor", content);
            content = HotkeySettingsToYaml(SettingsRepository<WorkspacesSettings>.GetInstance(new SettingsUtils()).SettingsConfig.Properties.Hotkey, "Workspaces", content);

            content += populateEndString;

            File.WriteAllText(path, content);
        }

        public static string HotkeySettingsToYaml(HotkeySettings hotkeySettings, string moduleName, string content, string? description = null)
        {
            content += "      - Name: " + moduleName + Environment.NewLine;
            content += "        Win: " + hotkeySettings.Win.ToString() + Environment.NewLine;
            content += "        Ctrl: " + hotkeySettings.Ctrl.ToString() + Environment.NewLine;
            content += "        Alt: " + hotkeySettings.Alt.ToString() + Environment.NewLine;
            content += "        Shift: " + hotkeySettings.Shift.ToString() + Environment.NewLine;
            if (description != null)
            {
                content += "        Description: " + description + Environment.NewLine;
            }

            content += "        Keys:" + Environment.NewLine;
            content += "          - " + hotkeySettings.Code.ToString(CultureInfo.InvariantCulture) + Environment.NewLine;
            return content;
        }

        public static string HotkeySettingsToYaml(KeyboardKeysProperty keyboardKeys, string moduleName, string content, string? description = null)
        {
            return HotkeySettingsToYaml(keyboardKeys.Value, moduleName, content, description);
        }
    }
}
