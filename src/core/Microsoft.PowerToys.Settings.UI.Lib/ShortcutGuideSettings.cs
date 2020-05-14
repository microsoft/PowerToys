// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.PowerToys.Settings.UI.Lib
{
    public class ShortcutGuideSettings
    {
        public ShortcutGuideSettings()
        {
            Name = "Shortcut Guide";
            Properties = new ShortcutGuideProperties();
            Version = "1.0";
        }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("properties")]
        public ShortcutGuideProperties Properties { get; set; }

        [JsonPropertyName("version")]
        public string Version { get; set; }

        public string ToJsonString()
        {
            return JsonSerializer.Serialize(this);
        }
    }
}
