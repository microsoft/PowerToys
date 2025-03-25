// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO.Abstractions;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using FancyZonesEditorCommon.Data;
using FancyZonesEditorCommon.Utils;
using static FancyZones.FuzzTests.FuzzTests;
using static FancyZonesEditorCommon.Data.CustomLayouts;

namespace FancyZones.FuzzTests
{
    public class FuzzTests
    {
        public static void FuzzGridFromJsonElement(ReadOnlySpan<byte> input)
        {
            if (input.Length < 4)
            {
                return;
            }

            int inputdata = BitConverter.ToInt32(input.Slice(0, 4));

            string mockCustomLayouts = $@"{{""custom-layouts"": [{{ 
            ""uuid"": ""{{B8C275E-A7BC-485F-A35C-67B69164F51F}}"",
                ""name"": ""Custom layout 1"",
                ""type"": ""grid"",
                ""info"": {{
                    ""rows"": {inputdata},
                    ""columns"": {inputdata},
                    ""rows-percentage"": [ {inputdata} ],
                    ""columns-percentage"": [ {inputdata}, {inputdata}, {inputdata} ],
                    ""cell-child-map"": [ [{inputdata}, {inputdata}, {inputdata}] ],
                    ""show-spacing"": true,
                    ""spacing"": {inputdata},
                    ""sensitivity-radius"": {inputdata}
                }}
            }}]}}";

            CustomLayoutListWrapper wrapper;
            try
            {
                wrapper = JsonSerializer.Deserialize<CustomLayoutListWrapper>(mockCustomLayouts, JsonOptions);
            }
            catch (JsonException)
            {
                return;
            }

            List<CustomLayouts.CustomLayoutWrapper> customLayouts = wrapper.CustomLayouts;

            if (customLayouts == null)
            {
                return;
            }

            foreach (var zoneSet in customLayouts)
            {
                if (zoneSet.Uuid == null || zoneSet.Uuid.Length == 0)
                {
                    return;
                }

                CustomLayouts deserializer = new CustomLayouts();
                _ = deserializer.GridFromJsonElement(zoneSet.Info.GetRawText());
            }
        }

        private static JsonSerializerOptions JsonOptions
        {
            get
            {
                return new JsonSerializerOptions
                {
                    PropertyNamingPolicy = new DashCaseNamingPolicy(),
                    WriteIndented = true,
                };
            }
        }
    }
}
