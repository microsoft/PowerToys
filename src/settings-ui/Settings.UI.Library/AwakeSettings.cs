// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using System.Text.Json.Serialization;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public class AwakeSettings : BasePTModuleSettings, ISettingsConfig, ICloneable
    {
        public const string ModuleName = "Awake";
        public const string ModuleVersion = "0.0.2";

        public AwakeSettings()
        {
            Name = ModuleName;
            Version = ModuleVersion;
            Properties = new AwakeProperties();
        }

        [JsonPropertyName("properties")]
        public AwakeProperties Properties { get; set; }

        public object Clone()
        {
            return new AwakeSettings()
            {
                Name = Name,
                Version = Version,
                Properties = new AwakeProperties()
                {
                    CustomTrayTimes = Properties.CustomTrayTimes.ToDictionary(entry => entry.Key, entry => entry.Value),
                    Mode = Properties.Mode,
                    KeepDisplayOn = Properties.KeepDisplayOn,
                    IntervalMinutes = Properties.IntervalMinutes,
                    IntervalHours = Properties.IntervalHours,

                    // Fix old buggy default value that might be saved in Settings. Some components don't deal well with negative time zones and minimum time offsets.
                    ExpirationDateTime = Properties.ExpirationDateTime.Year < 2 ? DateTimeOffset.Now : Properties.ExpirationDateTime,
                },
            };
        }

        public string GetModuleName()
        {
            return Name;
        }

        public bool UpgradeSettingsConfiguration()
        {
            return false;
        }
    }
}
