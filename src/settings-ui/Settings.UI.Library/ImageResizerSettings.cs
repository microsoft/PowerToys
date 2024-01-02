// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public class ImageResizerSettings : BasePTModuleSettings, ISettingsConfig
    {
        public const string ModuleName = "Image Resizer";

        private static readonly JsonSerializerOptions _serializerOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
        };

        [JsonPropertyName("properties")]
        public ImageResizerProperties Properties { get; set; }

        public ImageResizerSettings()
        {
            Version = "1";
            Name = ModuleName;
            Properties = new ImageResizerProperties();
        }

        public ImageResizerSettings(Func<string, string> resourceLoader)
            : this()
        {
            Properties = new ImageResizerProperties(resourceLoader);
        }

        public override string ToJsonString()
        {
            var options = _serializerOptions;
            return JsonSerializer.Serialize(this, options);
        }

        public string GetModuleName()
        {
            return Name;
        }

        // This can be utilized in the future if the settings.json file is to be modified/deleted.
        public bool UpgradeSettingsConfiguration()
        {
            return false;
        }
    }
}
