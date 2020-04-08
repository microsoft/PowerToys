// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.PowerToys.Settings.UI.Lib
{
    public class SndKeyboardManagerSettings
    {
        [JsonPropertyName("Keyboard Manager")]
        public KeyboardManagerSettings keyboardManagerSettings { get; }

        public SndKeyboardManagerSettings(KeyboardManagerSettings keyboardManagerSettings)
        {
            this.keyboardManagerSettings = keyboardManagerSettings;
        }

        public string ToJsonString()
        {
            return JsonSerializer.Serialize(this);
        }
    }
}
