// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics.Tracing;
using Microsoft.PowerToys.Telemetry;
using Microsoft.PowerToys.Telemetry.Events;

namespace AdvancedPaste.Telemetry
{
    [EventData]
    public class AdvancedPasteCustomFormatOutputThumbUpDownEvent : EventBase, IEvent
    {
        public bool PositiveFeedback { get; set; }

        public AdvancedPasteCustomFormatOutputThumbUpDownEvent(bool positiveFeedback)
        {
            PositiveFeedback = positiveFeedback;
        }

        public PartA_PrivTags PartA_PrivTags => PartA_PrivTags.ProductAndServiceUsage;
    }
}
