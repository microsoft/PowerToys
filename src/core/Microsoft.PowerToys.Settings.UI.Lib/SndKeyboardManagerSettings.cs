using System;
using System.Collections.Generic;
using System.Text;
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
