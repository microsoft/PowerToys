using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.PowerToys.Settings.UI.Lib
{
    public class FancyZonesSettingsIPCMessage
    {
        [JsonPropertyName("powertoys")]
        public SndFancyZonesSettings Powertoys { get; set; }

        public FancyZonesSettingsIPCMessage()
        {
        }

        public FancyZonesSettingsIPCMessage(SndFancyZonesSettings settings)
        {
            this.Powertoys = settings;
        }

        public string ToJsonString()
        {
            return JsonSerializer.Serialize(this);
        }
    }
}
