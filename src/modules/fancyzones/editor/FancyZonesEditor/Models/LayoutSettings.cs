// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using FancyZonesEditor.Models;

namespace FancyZonesEditor
{
    public class LayoutSettings
    {
        public static bool DefaultShowSpacing => true;

        public static int DefaultSpacing => 16;

        public static int DefaultZoneCount => 3;

        public static int DefaultSensitivityRadius => 20;

        public string ZonesetUuid { get; set; } = string.Empty;

        public LayoutType Type { get; set; } = LayoutType.PriorityGrid;

        public bool ShowSpacing { get; set; } = DefaultShowSpacing;

        public int Spacing { get; set; } = DefaultSpacing;

        public int ZoneCount { get; set; } = DefaultZoneCount;

        public int SensitivityRadius { get; set; } = DefaultSensitivityRadius;
    }
}
