// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

using Settings.UI.Library.Attributes;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public class AwakeProperties
    {
        public AwakeProperties()
        {
            KeepDisplayOn = false;
            Mode = AwakeMode.PASSIVE;
            IntervalHours = 0;
            IntervalMinutes = 1;
            ExpirationDateTime = DateTimeOffset.Now;
            CustomTrayTimes = [];

            // Defaults for activity-based mode
            ActivityCpuThresholdPercent = 20;
            ActivityMemoryThresholdPercent = 50;
            ActivityNetworkThresholdKBps = 100;
            ActivitySampleIntervalSeconds = 5;
            ActivityInactivityTimeoutSeconds = 60;
            
            // Usage tracking defaults (opt-in, disabled by default)
                        TrackUsageEnabled = false; // default off
            UsageRetentionDays = 14; // two weeks default retention
        }

        [JsonPropertyName("keepDisplayOn")]
        public bool KeepDisplayOn { get; set; }

        [JsonPropertyName("mode")]
        public AwakeMode Mode { get; set; }

        [JsonPropertyName("intervalHours")]
        public uint IntervalHours { get; set; }

        [JsonPropertyName("intervalMinutes")]
        public uint IntervalMinutes { get; set; }

        [JsonPropertyName("expirationDateTime")]
        public DateTimeOffset ExpirationDateTime { get; set; }

        [JsonPropertyName("customTrayTimes")]
        [CmdConfigureIgnore]
        public Dictionary<string, uint> CustomTrayTimes { get; set; }

        // Activity-based mode configuration
        [JsonPropertyName("activityCpuThresholdPercent")]
        public uint ActivityCpuThresholdPercent { get; set; }

        [JsonPropertyName("activityMemoryThresholdPercent")]
        public uint ActivityMemoryThresholdPercent { get; set; }

        [JsonPropertyName("activityNetworkThresholdKBps")]
        public uint ActivityNetworkThresholdKBps { get; set; }

        [JsonPropertyName("activitySampleIntervalSeconds")]
        public uint ActivitySampleIntervalSeconds { get; set; }

        [JsonPropertyName("activityInactivityTimeoutSeconds")]
        public uint ActivityInactivityTimeoutSeconds { get; set; }
        
        // New opt-in usage tracking flag
                [JsonPropertyName("trackUsageEnabled")]
        public bool TrackUsageEnabled { get; set; }

        // Retention window for usage data (days)
        [JsonPropertyName("usageRetentionDays")]
        public int UsageRetentionDays { get; set; }
    }
}
