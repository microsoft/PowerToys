// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public class SndMouseHighlighterSettings
    {
        [JsonPropertyName("MouseHighlighter")]
        public MouseHighlighterSettings MouseHighlighter { get; set; }

        public SndMouseHighlighterSettings()
        {
        }

        public SndMouseHighlighterSettings(MouseHighlighterSettings settings)
        {
            MouseHighlighter = settings;
        }

        public string ToJsonString()
        {
            return JsonSerializer.Serialize(this);
        }
    }
}
