// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;

using Microsoft.PowerToys.Settings.UI.Library.Interfaces;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public class VideoConferenceSettings : BasePTModuleSettings, ISettingsConfig
    {
        public const string ModuleName = "Video Conference";

        public VideoConferenceSettings()
        {
            Version = "1";
            Name = ModuleName;
            Properties = new VideoConferenceConfigProperties();
        }

        [JsonPropertyName("properties")]
        public VideoConferenceConfigProperties Properties { get; set; }

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
