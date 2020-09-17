// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;
using Microsoft.PowerToys.Settings.UI.Lib.Interface;

namespace Microsoft.PowerToys.Settings.UI.Lib
{
    public class ShortcutGuideSettings : BasePTModuleSettings, ISettingsConfig
    {
        public const string ModuleName = "Shortcut Guide";

        [JsonPropertyName("properties")]
        public ShortcutGuideProperties Properties { get; set; }

        public ShortcutGuideSettings()
        {
            Name = ModuleName;
            Properties = new ShortcutGuideProperties();
            Version = "1.0";
        }
    }
}
