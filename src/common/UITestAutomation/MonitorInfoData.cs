// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Microsoft.PowerToys.UITest
{
    public class MonitorInfoData
    {
        public MonitorInfoData()
        {
        }

        public struct MonitorInfoDataWrapper
        {
            public string DeviceName { get; set; }

            public string DeviceString { get; set; }

            public string DeviceID { get; set; }

            public string DeviceKey { get; set; }

            public int PelsWidth { get; set; }

            public int PelsHeight { get; set; }

            public int DisplayFrequency { get; set; }
        }

        public struct ParamsWrapper
        {
            [SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "This JSON DTO retains its writable deserialization contract, including existing omitted-member defaults and explicit-null behavior.")]
            public List<MonitorInfoDataWrapper> Monitors { get; set; }
        }
    }
}
