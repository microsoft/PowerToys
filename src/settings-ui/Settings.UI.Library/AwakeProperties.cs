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
            CustomTrayTimes = new Dictionary<string, int>();
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
        [CmdConfigureIgnoreAttribute]
        public Dictionary<string, int> CustomTrayTimes { get; set; }
    }

    public enum AwakeMode
    {
        PASSIVE = 0,
        INDEFINITE = 1,
        TIMED = 2,
        EXPIRABLE = 3,
    }
}
