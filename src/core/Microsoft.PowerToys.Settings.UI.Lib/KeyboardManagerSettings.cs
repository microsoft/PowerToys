// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Text;
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
            version = "1";
            name = "_unset_";
        }

        public KeyboardManagerSettings(string ptName)
        {
            Properties = new KeyboardManagerProperties();
            version = "1";
            name = ptName;
        }
    }
}
