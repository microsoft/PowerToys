// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using static FancyZonesEditorCommon.Data.LayoutTemplates;

namespace FancyZonesEditorCommon.Data
{
    public class LayoutTemplates : EditorData<TemplateLayoutsListWrapper>
    {
        public string File
        {
            get
            {
                return GetDataFolder() + "\\Microsoft\\PowerToys\\FancyZones\\layout-templates.json";
            }
        }

        public struct TemplateLayoutWrapper
        {
            public string Type { get; set; }

            public bool ShowSpacing { get; set; }

            public int Spacing { get; set; }

            public int ZoneCount { get; set; }

            public int SensitivityRadius { get; set; }
        }

        public struct TemplateLayoutsListWrapper
        {
            public List<TemplateLayoutWrapper> LayoutTemplates { get; set; }
        }
    }
}
