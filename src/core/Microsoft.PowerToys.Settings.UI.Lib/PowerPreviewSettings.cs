// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;

namespace Microsoft.PowerToys.Settings.UI.Lib
{
    public class PowerPreviewSettings : BasePTModuleSettings
    {
        public const string ModuleName = "File Explorer";

        [JsonPropertyName("Properties")]
        public PowerPreviewProperties Properties { get; set; }

        public PowerPreviewSettings()
        {
            Properties = new PowerPreviewProperties();
            Version = "1";
            Name = ModuleName;
        }

        public PowerPreviewSettings(string ptName)
        {
            Properties = new PowerPreviewProperties();
            Version = "1";
            Name = ptName;
        }
    }
}
