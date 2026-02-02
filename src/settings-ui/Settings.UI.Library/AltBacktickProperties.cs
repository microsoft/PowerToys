// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;
using Microsoft.PowerToys.Settings.UI.Library.Enumerations;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public class AltBacktickProperties
    {
        [JsonPropertyName("modifier_key")]
        public IntProperty ModifierKey { get; set; }

        [JsonPropertyName("ignore_minimized_windows")]
        public BoolProperty IgnoreMinimizedWindows { get; set; }

        public AltBacktickProperties()
        {
            ModifierKey = new IntProperty((int)AltBacktickModifierKey.Alt);
            IgnoreMinimizedWindows = new BoolProperty(true);
        }
    }
}
