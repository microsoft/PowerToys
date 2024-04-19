// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using FancyZonesEditor.Models;
using FancyZonesEditorCommon.Data;

namespace FancyZonesEditor
{
    public class LayoutSettings
    {
        public string ZonesetUuid { get; set; } = string.Empty;

        public LayoutType Type { get; set; } = LayoutType.PriorityGrid;

        public bool ShowSpacing { get; set; } = LayoutDefaultSettings.DefaultShowSpacing;

        public int Spacing { get; set; } = LayoutDefaultSettings.DefaultSpacing;

        public int ZoneCount { get; set; } = LayoutDefaultSettings.DefaultZoneCount;

        public int SensitivityRadius { get; set; } = LayoutDefaultSettings.DefaultSensitivityRadius;
    }
}
