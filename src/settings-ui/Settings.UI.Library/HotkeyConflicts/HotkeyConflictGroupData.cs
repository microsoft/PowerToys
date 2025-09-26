// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace Microsoft.PowerToys.Settings.UI.Library.HotkeyConflicts
{
    public class HotkeyConflictGroupData
    {
        public HotkeyData Hotkey { get; set; }

        public bool IsSystemConflict { get; set; }

        public bool ConflictIgnored { get; set; }

        public bool ConflictVisible => !ConflictIgnored;

        public bool ShouldShowSysConflict => !ConflictIgnored && IsSystemConflict;

        public List<ModuleHotkeyData> Modules { get; set; }
    }
}
