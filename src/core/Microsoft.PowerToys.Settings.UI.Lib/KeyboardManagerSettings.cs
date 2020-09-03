// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;

namespace Microsoft.PowerToys.Settings.UI.Lib
{
    public class KeyboardManagerSettings : BasePTModuleSettings
    {
        [JsonPropertyName("properties")]
        public KeyboardManagerProperties Properties { get; set; }

        public KeyboardManagerSettings()
        {
            Properties = new KeyboardManagerProperties();
            Version = "1";
            Name = "_unset_";
        }

        public KeyboardManagerSettings(string ptName)
        {
            Properties = new KeyboardManagerProperties();
            Version = "1";
            Name = ptName;
        }
    }
}
