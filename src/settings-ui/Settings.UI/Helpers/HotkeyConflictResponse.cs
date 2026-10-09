// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.PowerToys.Settings.UI.Library.HotkeyConflicts;

namespace Microsoft.PowerToys.Settings.UI.Helpers
{
    public class HotkeyConflictResponse
    {
        public string RequestId { get; set; }

        public bool HasConflict { get; set; }

        public List<ModuleHotkeyData> AllConflicts { get; set; } = new List<ModuleHotkeyData>();
    }
}
