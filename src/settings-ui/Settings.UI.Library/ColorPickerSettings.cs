﻿// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library.Enumerations;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public class ColorPickerSettings : BasePTModuleSettings, ISettingsConfig
    {
        public const string ModuleName = "ColorPicker";

        [JsonPropertyName("properties")]
        public ColorPickerProperties Properties { get; set; }

        public ColorPickerSettings()
        {
            Properties = new ColorPickerProperties();
            Version = "2.1";
            Name = ModuleName;
        }

        private static readonly JsonSerializerOptions _serializerOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
        };

        public virtual void Save(ISettingsUtils settingsUtils)
        {
            // Save settings to file
            var options = _serializerOptions;

            ArgumentNullException.ThrowIfNull(settingsUtils);

            settingsUtils.SaveSettings(JsonSerializer.Serialize(this, options), ModuleName);
        }

        public string GetModuleName()
            => Name;

        // This can be utilized in the future if the settings.json file is to be modified/deleted.
        public bool UpgradeSettingsConfiguration()
        {
            // Upgrading V1 to V2 doesn't set the version to 2.0, therefore V2 settings still report Version == 1.0
            if (Version == "1.0")
            {
                if (!Enum.IsDefined(Properties.ActivationAction))
                {
                    Properties.ActivationAction = ColorPickerActivationAction.OpenColorPicker;
                }

                Version = "2.1";
                return true;
            }

            return false;
        }

        public static object UpgradeSettings(object oldSettingsObject)
        {
            ColorPickerSettingsVersion1 oldSettings = (ColorPickerSettingsVersion1)oldSettingsObject;
            ColorPickerSettings newSettings = new ColorPickerSettings();
            newSettings.Properties.ActivationShortcut = oldSettings.Properties.ActivationShortcut;
            newSettings.Properties.ChangeCursor = oldSettings.Properties.ChangeCursor;
            newSettings.Properties.ActivationAction = oldSettings.Properties.ActivationAction;
            newSettings.Properties.ColorHistoryLimit = oldSettings.Properties.ColorHistoryLimit;
            newSettings.Properties.ShowColorName = oldSettings.Properties.ShowColorName;
            newSettings.Properties.ActivationShortcut = oldSettings.Properties.ActivationShortcut;
            newSettings.Properties.VisibleColorFormats = new Dictionary<string, KeyValuePair<bool, string>>();
            foreach (KeyValuePair<string, bool> oldValue in oldSettings.Properties.VisibleColorFormats)
            {
                newSettings.Properties.VisibleColorFormats.Add(oldValue.Key, new KeyValuePair<bool, string>(oldValue.Value, ColorFormatHelper.GetDefaultFormat(oldValue.Key)));
            }

            newSettings.Properties.CopiedColorRepresentation = newSettings.Properties.VisibleColorFormats.ElementAt((int)oldSettings.Properties.CopiedColorRepresentation).Key;
            return newSettings;
        }
    }
}
