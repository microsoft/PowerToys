// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public class CmdNotFoundSettings : BasePTModuleSettings, ISettingsConfig
    {
        private static readonly JsonSerializerOptions SerializerOptions = new()
        {
            WriteIndented = true,
        };

        public const string ModuleName = "CmdNotFound";

        public CmdNotFoundSettings()
        {
            Version = "1";
            Name = ModuleName;
        }

        public virtual void Save(ISettingsUtils settingsUtils)
        {
            // Save settings to file
            ArgumentNullException.ThrowIfNull(settingsUtils);

            settingsUtils.SaveSettings(JsonSerializer.Serialize(this, SerializerOptions), ModuleName);
        }

        public string GetModuleName()
            => Name;

        // This can be utilized in the future if the settings.json file is to be modified/deleted.
        public bool UpgradeSettingsConfiguration()
            => false;
    }
}
