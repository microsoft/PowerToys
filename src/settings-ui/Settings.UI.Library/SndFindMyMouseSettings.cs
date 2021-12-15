// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public class SndFindMyMouseSettings
    {
        [JsonPropertyName("FindMyMouse")]
        public FindMyMouseSettings FindMyMouse { get; set; }

        public SndFindMyMouseSettings()
        {
        }

        public SndFindMyMouseSettings(FindMyMouseSettings settings)
        {
            FindMyMouse = settings;
        }

        public string ToJsonString()
        {
            return JsonSerializer.Serialize(this);
        }
    }
}
