// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.PowerToys.Settings.UI.Lib
{
    public class PowerPreviewProperties
    {
        [JsonPropertyName("svg-previewer-toggle-setting")]
        public BoolProperty EnableSvg { get; set; }

        [JsonPropertyName("md-previewer-toggle-setting")]
        public BoolProperty EnableMd { get; set; }

        public PowerPreviewProperties()
        {
            EnableSvg = new BoolProperty();
            EnableMd = new BoolProperty();
        }

        public override string ToString()
        {
            return JsonSerializer.Serialize(this);
        }
    }
}
