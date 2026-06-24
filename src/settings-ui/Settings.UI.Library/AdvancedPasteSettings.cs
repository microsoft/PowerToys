// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public class AdvancedPasteSettings : BasePTModuleSettings, ISettingsConfig, IHotkeyConfig
    {
        public const string ModuleName = "AdvancedPaste";

        private static readonly JsonSerializerOptions _serializerOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
        };

        [JsonPropertyName("properties")]
        public AdvancedPasteProperties Properties { get; set; }

        public AdvancedPasteSettings()
        {
            Properties = new AdvancedPasteProperties();
            Version = "1";
            Name = ModuleName;
        }

        public virtual void Save(SettingsUtils settingsUtils)
        {
            // Save settings to file
            var options = _serializerOptions;

            ArgumentNullException.ThrowIfNull(settingsUtils);

            settingsUtils.SaveSettings(JsonSerializer.Serialize(this, options), ModuleName);
        }

        public ModuleType GetModuleType() => ModuleType.AdvancedPaste;

        public HotkeyAccessor[] GetAllHotkeyAccessors()
        {
            var hotkeyAccessors = new List<HotkeyAccessor>
            {
                new HotkeyAccessor(
                    () => Properties.PasteAsPlainTextShortcut,
                    value => Properties.PasteAsPlainTextShortcut = value ?? AdvancedPasteProperties.DefaultPasteAsPlainTextShortcut,
                    "PasteAsPlainText_Shortcut"),
                new HotkeyAccessor(
                    () => Properties.AdvancedPasteUIShortcut,
                    value => Properties.AdvancedPasteUIShortcut = value ?? AdvancedPasteProperties.DefaultAdvancedPasteUIShortcut,
                    "AdvancedPasteUI_Shortcut"),
                new HotkeyAccessor(
                    () => Properties.PasteAsMarkdownShortcut,
                    value => Properties.PasteAsMarkdownShortcut = value ?? new HotkeySettings(),
                    "PasteAsMarkdown_Shortcut"),
                new HotkeyAccessor(
                    () => Properties.PasteAsJsonShortcut,
                    value => Properties.PasteAsJsonShortcut = value ?? new HotkeySettings(),
                    "PasteAsJson_Shortcut"),
            };

            string[] additionalActionHeaderKeys =
            [
                "ImageToText",
                "PasteAsTxtFile",
                "PasteAsPngFile",
                "PasteAsHtmlFile",
                "TranscodeToMp3",
                "TranscodeToMp4",
            ];
            int index = 0;
            foreach (var action in Properties.AdditionalActions.GetAllActions())
            {
                if (action is AdvancedPasteAdditionalAction additionalAction)
                {
                    hotkeyAccessors.Add(new HotkeyAccessor(
                        () => additionalAction.Shortcut,
                        value => additionalAction.Shortcut = value ?? new HotkeySettings(),
                        additionalActionHeaderKeys[index]));
                    index++;
                }
            }

            // Custom actions do not have localization header, just use the action name.
            foreach (var customAction in Properties.CustomActions.Value)
            {
                hotkeyAccessors.Add(new HotkeyAccessor(
                    () => customAction.Shortcut,
                    value => customAction.Shortcut = value ?? new HotkeySettings(),
                    customAction.Name));
            }

            return hotkeyAccessors.ToArray();
        }

        public string GetModuleName()
            => Name;

        // This can be utilized in the future if the settings.json file is to be modified/deleted.
        public bool UpgradeSettingsConfiguration()
            => false;
    }
}
