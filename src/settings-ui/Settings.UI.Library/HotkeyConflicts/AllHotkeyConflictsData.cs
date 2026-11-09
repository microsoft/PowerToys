// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.PowerToys.Settings.UI.Library.HotkeyConflicts
{
    public class AllHotkeyConflictsData
    {
        public List<HotkeyConflictGroupData> InAppConflicts { get; init; } = new List<HotkeyConflictGroupData>();

        public List<HotkeyConflictGroupData> SystemConflicts { get; init; } = new List<HotkeyConflictGroupData>();
    }
}
