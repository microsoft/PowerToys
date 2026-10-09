// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public class ShortcutConflictProperties
    {
        [JsonPropertyName("ignored_shortcuts")]
        public List<HotkeySettings> IgnoredShortcuts { get; set; }

        public ShortcutConflictProperties()
        {
            IgnoredShortcuts = new List<HotkeySettings>();
        }
    }
}
