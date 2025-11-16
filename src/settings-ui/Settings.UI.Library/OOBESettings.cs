// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;

namespace Settings.UI.Library
{
    public class OOBESettings : ISettingsConfig
    {
        [JsonPropertyName("openedAtFirstLaunch")]
        public bool OpenedAtFirstLaunch { get; set; }

        public string GetModuleName()
        {
            return "OOBE";
        }

        public string ToJsonString()
        {
            return JsonSerializer.Serialize(this);
        }

        public bool UpgradeSettingsConfiguration()
        {
            return false;
        }
    }
}
