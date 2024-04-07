// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Text.Json;
using static FancyZonesEditorCommon.Data.CustomLayouts;

namespace FancyZonesEditorCommon.Data
{
    public class CustomLayouts : EditorData<CustomLayoutListWrapper>
    {
        public string File
        {
            get
            {
                return GetDataFolder() + "\\Microsoft\\PowerToys\\FancyZones\\custom-layouts.json";
            }
        }

        public sealed class CanvasInfoWrapper
        {
            public struct CanvasZoneWrapper
            {
                public int X { get; set; }

                public int Y { get; set; }

                public int Width { get; set; }

                public int Height { get; set; }
            }

            public int RefWidth { get; set; }

            public int RefHeight { get; set; }

            public List<CanvasZoneWrapper> Zones { get; set; }

            public int SensitivityRadius { get; set; } = LayoutDefaultSettings.DefaultSensitivityRadius;
        }

        public sealed class GridInfoWrapper
        {
            public int Rows { get; set; }

            public int Columns { get; set; }

            public List<int> RowsPercentage { get; set; }

            public List<int> ColumnsPercentage { get; set; }

            public int[][] CellChildMap { get; set; }

            public bool ShowSpacing { get; set; } = LayoutDefaultSettings.DefaultShowSpacing;

            public int Spacing { get; set; } = LayoutDefaultSettings.DefaultSpacing;

            public int SensitivityRadius { get; set; } = LayoutDefaultSettings.DefaultSensitivityRadius;
        }

        public struct CustomLayoutWrapper
        {
            public string Uuid { get; set; }

            public string Name { get; set; }

            public string Type { get; set; }

            public JsonElement Info { get; set; } // CanvasInfoWrapper or GridInfoWrapper
        }

        public struct CustomLayoutListWrapper
        {
            public List<CustomLayoutWrapper> CustomLayouts { get; set; }
        }

        public JsonElement ToJsonElement(CanvasInfoWrapper info)
        {
            string json = JsonSerializer.Serialize(info, this.JsonOptions);
            return JsonSerializer.Deserialize<JsonElement>(json);
        }

        public JsonElement ToJsonElement(GridInfoWrapper info)
        {
            string json = JsonSerializer.Serialize(info, this.JsonOptions);
            return JsonSerializer.Deserialize<JsonElement>(json);
        }

        public CanvasInfoWrapper CanvasFromJsonElement(string json)
        {
            return JsonSerializer.Deserialize<CanvasInfoWrapper>(json, this.JsonOptions);
        }

        public GridInfoWrapper GridFromJsonElement(string json)
        {
            return JsonSerializer.Deserialize<GridInfoWrapper>(json, this.JsonOptions);
        }
    }
}
