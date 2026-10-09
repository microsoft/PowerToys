// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Text.Json;
using static FancyZonesEditorCommon.Data.AppZoneHistory.AppZoneHistoryWrapper;
using static FancyZonesEditorCommon.Data.CustomLayouts;

namespace FancyZonesEditorCommon.Data
{
    public class AppZoneHistory : EditorData<AppZoneHistory.AppZoneHistoryListWrapper>
    {
        public string File
        {
            get
            {
                return GetDataFolder() + "\\Microsoft\\PowerToys\\FancyZones\\app-zone-history.json";
            }
        }

        public struct AppZoneHistoryWrapper
        {
            public struct ZoneHistoryWrapper
            {
                public int[][] ZoneIndexSet { get; set; }

                public DeviceIdWrapper Device { get; set; }

                public string ZonesetUuid { get; set; }
            }

            public struct DeviceIdWrapper
            {
                public string Monitor { get; set; }

                public string MonitorInstance { get; set; }

                public int MonitorNumber { get; set; }

                public string SerialNumber { get; set; }

                public string VirtualDesktop { get; set; }
            }

            public string AppPath { get; set; }

            public List<ZoneHistoryWrapper> History { get; set; }
        }

        public struct AppZoneHistoryListWrapper
        {
            public List<AppZoneHistoryWrapper> AppZoneHistory { get; set; }
        }

        public JsonElement ToJsonElement(ZoneHistoryWrapper info)
        {
            string json = JsonSerializer.Serialize(info, JsonOptions);
            return JsonSerializer.Deserialize<JsonElement>(json);
        }

        public JsonElement ToJsonElement(DeviceIdWrapper info)
        {
            string json = JsonSerializer.Serialize(info, JsonOptions);
            return JsonSerializer.Deserialize<JsonElement>(json);
        }

        public ZoneHistoryWrapper ZoneHistoryFromJsonElement(string json)
        {
            return JsonSerializer.Deserialize<ZoneHistoryWrapper>(json, JsonOptions);
        }

        public DeviceIdWrapper GridFromJsonElement(string json)
        {
            return JsonSerializer.Deserialize<DeviceIdWrapper>(json, JsonOptions);
        }
    }
}
