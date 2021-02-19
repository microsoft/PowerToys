// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics.Tracing;
using Microsoft.PowerToys.Telemetry;
using Microsoft.PowerToys.Telemetry.Events;

namespace Microsoft.PowerToys.Settings.UI.Library.Telemetry.Events
{
    [EventData]
    public class PluginSettingsCompleteEvent : EventBase, IEvent
    {
        public string Name { get; set; }

        public bool Disabled { get; set; }

        public bool IsGlobal { get; set; }

        public string ActionKeyword { get; set; }

        public PartA_PrivTags PartA_PrivTags => PartA_PrivTags.ProductAndServiceUsage;
    }
}
