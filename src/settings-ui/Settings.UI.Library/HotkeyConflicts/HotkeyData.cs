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
    public class HotkeyData
    {
        public bool Win { get; set; }

        public bool Ctrl { get; set; }

        public bool Shift { get; set; }

        public bool Alt { get; set; }

        public int Key { get; set; }
    }
}
