// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using FancyZonesEditor.Models;

namespace FancyZonesEditor
{
    public class LayoutSettings
    {
        // TODO: share the constants b/w C# Editor and FancyZoneLib
        public const bool DefaultShowSpacing = true;

        public const int DefaultSpacing = 16;

        public const int DefaultZoneCount = 3;

        public const int DefaultSensitivityRadius = 20;

        public const int MaxZones = 128;

        public string ZonesetUuid { get; set; } = string.Empty;

        public LayoutType Type { get; set; } = LayoutType.PriorityGrid;

        public bool ShowSpacing { get; set; } = DefaultShowSpacing;

        public int Spacing { get; set; } = DefaultSpacing;

        public int ZoneCount { get; set; } = DefaultZoneCount;

        public int SensitivityRadius { get; set; } = DefaultSensitivityRadius;
    }
}
