// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using static FancyZonesEditorCommon.Data.DefaultLayouts;

namespace FancyZonesEditorCommon.Data
{
    public class DefaultLayouts : EditorData<DefaultLayoutsListWrapper>
    {
        public string File
        {
            get
            {
                return GetDataFolder() + "\\Microsoft\\PowerToys\\FancyZones\\default-layouts.json";
            }
        }

        public struct DefaultLayoutWrapper
        {
            public struct LayoutWrapper
            {
                public string Uuid { get; set; }

                public string Type { get; set; }

                public bool ShowSpacing { get; set; }

                public int Spacing { get; set; }

                public int ZoneCount { get; set; }

                public int SensitivityRadius { get; set; }
            }

            public string MonitorConfiguration { get; set; }

            public LayoutWrapper Layout { get; set; }
        }

        public struct DefaultLayoutsListWrapper
        {
            public List<DefaultLayoutWrapper> DefaultLayouts { get; set; }
        }
    }
}
