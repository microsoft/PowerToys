// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics.Tracing;
using Microsoft.PowerToys.Telemetry;
using Microsoft.PowerToys.Telemetry.Events;

namespace Peek.UI.Telemetry.Events
{
    [EventData]
    public class OpenedEvent : EventBase, IEvent
    {
        public OpenedEvent()
        {
            EventName = "Peek_Opened";
        }

        public string ActivationKind { get; set; } = string.Empty;

        public string FileExtension { get; set; } = string.Empty;

        public bool IsAppToggledOn { get; set; }

        public double HotKeyToVisibleTimeMs { get; set; }

        public PartA_PrivTags PartA_PrivTags => PartA_PrivTags.ProductAndServiceUsage;
    }
}
