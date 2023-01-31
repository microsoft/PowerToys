// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics.Tracing;
using Microsoft.PowerToys.Telemetry;
using Microsoft.PowerToys.Telemetry.Events;

namespace PastePlain.Telemetry
{
    [EventData]
    public class PastePlainSettings : EventBase, IEvent
    {
        public PastePlainSettings(string shortcut)
        {
            ActivationShortcut = shortcut;
            EventName = "PastePlain_Settings";
        }

        public string ActivationShortcut { get; set; }

        public PartA_PrivTags PartA_PrivTags => PartA_PrivTags.ProductAndServiceUsage;
    }
}
