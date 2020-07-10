using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.PowerToys.Settings.UI.Lib
{
    public class VideoConferenceSettingsIPCMessage
    {
        [JsonPropertyName("powertoys")]
        public SndVideoConferenceSettings Powertoys { get; set; }

        public VideoConferenceSettingsIPCMessage()
        {

        }

        public VideoConferenceSettingsIPCMessage(SndVideoConferenceSettings settings)
        {
            this.Powertoys = settings;
        }

        public string ToJsonString()
        {
            return JsonSerializer.Serialize(this);
        }
    }
}
