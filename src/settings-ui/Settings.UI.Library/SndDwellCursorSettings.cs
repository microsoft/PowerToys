// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public class SndDwellCursorSettings
    {
        [JsonPropertyName("DwellCursor")]
        public DwellCursorSettings DwellCursor { get; set; }

        public SndDwellCursorSettings()
        {
        }

        public SndDwellCursorSettings(DwellCursorSettings s)
        {
            DwellCursor = s;
        }

        public string ToJsonString() => System.Text.Json.JsonSerializer.Serialize(this);
    }
}
