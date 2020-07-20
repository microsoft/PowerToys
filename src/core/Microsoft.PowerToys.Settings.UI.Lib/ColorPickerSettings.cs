// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;

namespace Microsoft.PowerToys.Settings.UI.Lib
{
    public class ColorPickerSettings : BasePTModuleSettings
    {
        public const string ModuleName = "ColorPicker";

        public ColorPickerProperties properties { get; set; }

        public ColorPickerSettings()
        {
            properties = new ColorPickerProperties();
            version = "1";
            name = ModuleName;
        }

        public override string ToJsonString()
        {
            return JsonSerializer.Serialize(this);
        }

        public virtual void Save()
        {
            // Save settings to file
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
            };

            SettingsUtils.SaveSettings(JsonSerializer.Serialize(this, options), ModuleName);
        }
    }
}
