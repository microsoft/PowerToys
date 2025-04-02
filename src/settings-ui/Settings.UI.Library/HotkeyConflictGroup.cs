// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public class HotkeyConflictGroup
    {
        public HotkeySettings Hotkey { get; set; } = new HotkeySettings();

        public List<ModuleHotkeyInfo> Modules { get; set; } = new List<ModuleHotkeyInfo>();

        public bool IsSystemConflict { get; set; }
    }
}
