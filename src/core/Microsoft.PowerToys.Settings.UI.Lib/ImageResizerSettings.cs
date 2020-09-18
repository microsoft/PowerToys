// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.PowerToys.Settings.UI.Lib.Interface;

namespace Microsoft.PowerToys.Settings.UI.Lib
{
    public class ImageResizerSettings : BasePTModuleSettings, ISettingsConfig
    {
        public const string ModuleName = "Image Resizer";

        [JsonPropertyName("properties")]
        public ImageResizerProperties Properties { get; set; }

        public ImageResizerSettings()
        {
            Version = "1";
            Name = ModuleName;
            Properties = new ImageResizerProperties();
        }

        public override string ToJsonString()
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
            };
            return JsonSerializer.Serialize(this, options);
        }
    }
}
