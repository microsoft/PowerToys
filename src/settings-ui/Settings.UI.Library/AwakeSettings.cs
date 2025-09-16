// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using System.Reflection;
using System.Text.Json.Serialization;

using Microsoft.PowerToys.Settings.UI.Library.Interfaces;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public class AwakeSettings : BasePTModuleSettings, ISettingsConfig, ICloneable
    {
        public const string ModuleName = "Awake";

        public AwakeSettings()
        {
            Name = ModuleName;
            Version = Assembly.GetExecutingAssembly().GetName().Version.ToString();
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
                    ActivityCpuThresholdPercent = Properties.ActivityCpuThresholdPercent,
                    ActivityMemoryThresholdPercent = Properties.ActivityMemoryThresholdPercent,
                    ActivityNetworkThresholdKBps = Properties.ActivityNetworkThresholdKBps,
                    ActivitySampleIntervalSeconds = Properties.ActivitySampleIntervalSeconds,
                    ActivityInactivityTimeoutSeconds = Properties.ActivityInactivityTimeoutSeconds,

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
