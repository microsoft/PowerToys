// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

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
            public List<MonitorInfoDataWrapper> Monitors { get; set; }
        }
    }
}
