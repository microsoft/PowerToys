// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.PowerToys.Settings.UI.Lib
{
    public class EnabledModules
    {
        public EnabledModules()
        {
            this.FancyZones = false;
            this.ImageResizer = false;
            this.FileExplorerPreview = false;
            this.PowerRename = false;
            this.ShortcutGuide = false;
        }

        [JsonPropertyName("FancyZones")]
        public bool FancyZones { get; set; }

        [JsonPropertyName("Image Resizer")]
        public bool ImageResizer { get; set; }

        [JsonPropertyName("File Explorer Preview")]
        public bool FileExplorerPreview { get; set; }

        [JsonPropertyName("Shortcut Guide")]
        public bool ShortcutGuide { get; set; }

        public bool PowerRename { get; set; }

        public string ToJsonString()
        {
            return JsonSerializer.Serialize(this);
        }
    }
}