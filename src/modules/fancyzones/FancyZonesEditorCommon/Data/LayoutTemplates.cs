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
                return FancyZonesPaths.LayoutTemplates;
            }
        }

        public struct TemplateLayoutWrapper
        {
            public string Type { get; set; }

            public bool ShowSpacing { get; set; }

            public int Spacing { get; set; }

            public int ZoneCount { get; set; }

            public int SensitivityRadius { get; set; }

            // Zone (or inclusive zone range) a newly created window with no zone history falls back
            // to on this layout. Null means the feature is off for this layout. Zero-based, same as
            // the zone indexes already used elsewhere (e.g. app zone history).
            public List<int> DefaultZoneSet { get; set; }
        }

        public struct TemplateLayoutsListWrapper
        {
            public List<TemplateLayoutWrapper> LayoutTemplates { get; set; }
        }
    }
}
