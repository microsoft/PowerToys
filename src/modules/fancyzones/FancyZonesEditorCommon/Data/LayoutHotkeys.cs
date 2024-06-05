// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using static FancyZonesEditorCommon.Data.LayoutHotkeys;

namespace FancyZonesEditorCommon.Data
{
    public class LayoutHotkeys : EditorData<LayoutHotkeysWrapper>
    {
        public string File
        {
            get
            {
                return GetDataFolder() + "\\Microsoft\\PowerToys\\FancyZones\\layout-hotkeys.json";
            }
        }

        public struct LayoutHotkeyWrapper
        {
            public int Key { get; set; }

            public string LayoutId { get; set; }
        }

        public struct LayoutHotkeysWrapper
        {
            public List<LayoutHotkeyWrapper> LayoutHotkeys { get; set; }
        }
    }
}
