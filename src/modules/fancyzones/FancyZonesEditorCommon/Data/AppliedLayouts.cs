// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace FancyZonesEditorCommon.Data
{
    public class AppliedLayouts : EditorData<AppliedLayouts.AppliedLayoutsListWrapper>
    {
        public string File
        {
            get
            {
                return GetDataFolder() + "\\Microsoft\\PowerToys\\FancyZones\\applied-layouts.json";
            }
        }

        public struct AppliedLayoutWrapper
        {
            public struct DeviceIdWrapper
            {
                public string Monitor { get; set; }

                public string MonitorInstance { get; set; }

                public int MonitorNumber { get; set; }

                public string SerialNumber { get; set; }

                public string VirtualDesktop { get; set; }
            }

            public struct LayoutWrapper
            {
                public string Uuid { get; set; }

                public string Type { get; set; }

                public bool ShowSpacing { get; set; }

                public int Spacing { get; set; }

                public int ZoneCount { get; set; }

                public int SensitivityRadius { get; set; }
            }

            public DeviceIdWrapper Device { get; set; }

            public LayoutWrapper AppliedLayout { get; set; }
        }

        public struct AppliedLayoutsListWrapper
        {
            public List<AppliedLayoutWrapper> AppliedLayouts { get; set; }
        }
    }
}
