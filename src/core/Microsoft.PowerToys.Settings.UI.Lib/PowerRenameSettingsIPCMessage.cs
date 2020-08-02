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
    public class PowerRenameSettingsIPCMessage
    {
        [JsonPropertyName("powertoys")]
        public SndPowerRenameSettings Powertoys { get; set; }

        public PowerRenameSettingsIPCMessage()
        {
        }

        public PowerRenameSettingsIPCMessage(SndPowerRenameSettings settings)
        {
            this.Powertoys = settings;
        }

        public string ToJsonString()
        {
            return JsonSerializer.Serialize(this);
        }
    }
}
