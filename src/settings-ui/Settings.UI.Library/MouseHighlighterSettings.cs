// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.Json.Serialization;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public class MouseHighlighterSettings : BasePTModuleSettings, ISettingsConfig
    {
        public const string ModuleName = "MouseHighlighter";

        [JsonPropertyName("properties")]
        public MouseHighlighterProperties Properties { get; set; }

        public MouseHighlighterSettings()
        {
            Name = ModuleName;
            Properties = new MouseHighlighterProperties();
            Version = "1.2";
        }

        public string GetModuleName()
        {
            return Name;
        }

        // This can be utilized in the future if the settings.json file is to be modified/deleted.
        public bool UpgradeSettingsConfiguration()
        {
            if (Version == "1.0" || Version == "1.1")
            {
                string opacity;
                if (Version == "1.0")
                {
                    opacity = string.Format(CultureInfo.InvariantCulture, "{0:X2}", Properties.HighlightOpacity.Value);
                }
                else
                {
                    // 1.1
                    opacity = string.Format(CultureInfo.InvariantCulture, "{0:X2}", Properties.HighlightOpacity.Value * 255 / 100);
                }

                Properties.LeftButtonClickColor = new StringProperty(string.Concat("#", opacity, Properties.LeftButtonClickColor.Value.ToString().Substring(1, 6)));
                Properties.RightButtonClickColor = new StringProperty(string.Concat("#", opacity, Properties.RightButtonClickColor.Value.ToString().Substring(1, 6)));
                Version = "1.2";
                return true;
            }

            return false;
        }
    }
}
