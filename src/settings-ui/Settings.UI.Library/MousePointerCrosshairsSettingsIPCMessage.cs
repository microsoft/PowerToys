// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public class MousePointerCrosshairsSettingsIPCMessage
    {
        [JsonPropertyName("powertoys")]
        public SndMousePointerCrosshairsSettings Powertoys { get; set; }

        public MousePointerCrosshairsSettingsIPCMessage()
        {
        }

        public MousePointerCrosshairsSettingsIPCMessage(SndMousePointerCrosshairsSettings settings)
        {
            this.Powertoys = settings;
        }

        public string ToJsonString()
        {
            return JsonSerializer.Serialize(this);
        }
    }
}
