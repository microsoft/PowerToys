// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public class PowerRenameProperties
    {
        public PowerRenameProperties()
        {
            PersistState = new BoolProperty();
            MRUEnabled = new BoolProperty();
            MaxMRUSize = new IntProperty();
            ShowIcon = new BoolProperty();
            ExtendedContextMenuOnly = new BoolProperty();
            UseBoostLib = new BoolProperty();
            Enabled = new BoolProperty();
        }

        public BoolProperty Enabled { get; set; }

        [JsonPropertyName("bool_persist_input")]
        public BoolProperty PersistState { get; set; }

        [JsonPropertyName("bool_mru_enabled")]
        public BoolProperty MRUEnabled { get; set; }

        [JsonPropertyName("int_max_mru_size")]
        public IntProperty MaxMRUSize { get; set; }

        [JsonPropertyName("bool_show_icon_on_menu")]
        public BoolProperty ShowIcon { get; set; }

        [JsonPropertyName("bool_show_extended_menu")]
        public BoolProperty ExtendedContextMenuOnly { get; set; }

        [JsonPropertyName("bool_use_boost_lib")]
        public BoolProperty UseBoostLib { get; set; }
    }
}
