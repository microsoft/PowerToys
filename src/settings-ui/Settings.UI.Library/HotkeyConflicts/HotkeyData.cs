// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.PowerToys.Settings.UI.Library.Utilities;

namespace Microsoft.PowerToys.Settings.UI.Library.HotkeyConflicts
{
    public class HotkeyData
    {
        public bool Win { get; set; }

        public bool Ctrl { get; set; }

        public bool Shift { get; set; }

        public bool Alt { get; set; }

        public int Key { get; set; }

        public List<object> GetKeysList()
        {
            List<object> shortcutList = new List<object>();

            if (Win)
            {
                shortcutList.Add(92); // The Windows key or button.
            }

            if (Ctrl)
            {
                shortcutList.Add("Ctrl");
            }

            if (Alt)
            {
                shortcutList.Add("Alt");
            }

            if (Shift)
            {
                shortcutList.Add(16); // The Shift key or button.
            }

            if (Key > 0)
            {
                switch (Key)
                {
                    // https://learn.microsoft.com/uwp/api/windows.system.virtualkey?view=winrt-20348
                    case 38: // The Up Arrow key or button.
                    case 40: // The Down Arrow key or button.
                    case 37: // The Left Arrow key or button.
                    case 39: // The Right Arrow key or button.
                        shortcutList.Add(Key);
                        break;
                    default:
                        var localKey = Helper.GetKeyName((uint)Key);
                        shortcutList.Add(localKey);
                        break;
                }
            }

            return shortcutList;
        }
    }
}
