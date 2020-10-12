// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.PowerToys.Settings.UI.Lib.Interface;

namespace Microsoft.PowerToys.Settings.UI.Lib
{
    public class VideoConferenceSettings : BasePTModuleSettings, ISettingsConfig
    {
        public VideoConferenceSettings()
        {
            Version = "1";
            Name = "Video Conference";
            Properties = new VideoConferenceConfigProperties();
        }

        [JsonPropertyName("properties")]
        public VideoConferenceConfigProperties Properties { get; set; }

        public string GetModuleName()
        {
            return Name;
        }

        bool ISettingsConfig.UpgradeSettingsConfiguration()
        {
            return false;
        }
    }
}
