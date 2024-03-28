// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using Settings.UI.Library.Attributes;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public class KeyboardManagerProperties
    {
        [JsonPropertyName("activeConfiguration")]
        [CmdConfigureIgnoreAttribute]
        public GenericProperty<string> ActiveConfiguration { get; set; }

        // List of all Keyboard Configurations.
        [JsonPropertyName("keyboardConfigurations")]
        [CmdConfigureIgnoreAttribute]
        public GenericProperty<List<string>> KeyboardConfigurations { get; set; }

        public KeyboardManagerProperties()
        {
            KeyboardConfigurations = new GenericProperty<List<string>>(new List<string> { "default", });
            ActiveConfiguration = new GenericProperty<string>("default");
        }

        public string ToJsonString()
        {
            return JsonSerializer.Serialize(this);
        }
    }
}
