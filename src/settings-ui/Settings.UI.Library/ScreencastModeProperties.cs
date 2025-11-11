// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public class ScreencastModeProperties
    {
        public HotkeySettings ScreencastModeShortcut { get; set; }

        public ScreencastModeProperties()
        {
            ScreencastModeShortcut = new HotkeySettings(true, false, true, false, 83); // Win + Alt + S
        }
    }
}
