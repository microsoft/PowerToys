// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics.Tracing;
using Microsoft.PowerToys.Telemetry;
using Microsoft.PowerToys.Telemetry.Events;

namespace Microsoft.PowerLauncher.Telemetry
{
    /// <summary>
    /// ETW Event for when the user initiates a query
    /// </summary>
    [EventData]
    public class LauncherQueryEvent : EventBase, IEvent
    {
        public double QueryTimeMs { get; set; }

        public int QueryLength { get; set; }

        public int NumResults { get; set; }

        public PartA_PrivTags PartA_PrivTags => PartA_PrivTags.ProductAndServicePerformance;
    }
}
