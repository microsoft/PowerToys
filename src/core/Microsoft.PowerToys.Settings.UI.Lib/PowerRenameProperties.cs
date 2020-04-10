// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;

namespace Microsoft.PowerToys.Settings.UI.Lib
{
    public class PowerRenameProperties
    {
        public PowerRenameProperties()
        {
            PersistInput = new BoolProperty();
            MruEnabled = new BoolProperty();
            MaxMruSize = new IntProperty();
            ShowIconInMenu = new BoolProperty();
            ShowExtendedMenu = new BoolProperty();
        }

        [JsonPropertyName("bool_persist_input")]
        public BoolProperty PersistInput { get; set; }

        [JsonPropertyName("bool_mru_enabled")]
        public BoolProperty MruEnabled { get; set; }

        [JsonPropertyName("int_max_mru_size")]
        public IntProperty MaxMruSize { get; set; }

        [JsonPropertyName("bool_show_icon_on_menu")]
        public BoolProperty ShowIconInMenu { get; set; }

        [JsonPropertyName("bool_show_extended_menu")]
        public BoolProperty ShowExtendedMenu { get; set; }
    }
}
