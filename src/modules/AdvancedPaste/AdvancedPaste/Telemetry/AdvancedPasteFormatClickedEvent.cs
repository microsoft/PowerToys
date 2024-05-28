// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics.Tracing;
using AdvancedPaste.Models;
using Microsoft.PowerToys.Telemetry;
using Microsoft.PowerToys.Telemetry.Events;

namespace AdvancedPaste.Telemetry
{
    [EventData]
    public class AdvancedPasteFormatClickedEvent : EventBase, IEvent
    {
        public PasteFormats PasteFormat { get; set; }

        public AdvancedPasteFormatClickedEvent(PasteFormats format)
        {
            this.PasteFormat = format;
        }

        public PartA_PrivTags PartA_PrivTags => PartA_PrivTags.ProductAndServiceUsage;
    }
}
