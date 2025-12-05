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
    }
}
