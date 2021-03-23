// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics.Tracing;
using Microsoft.PowerToys.Telemetry;
using Microsoft.PowerToys.Telemetry.Events;

namespace ColorPicker.Telemetry
{
    [EventData]
    public class ColorPickerSessionEvent : EventBase, IEvent
    {
        public string StartedAs { get; set; }

        public bool ZoomUsed { get; set; }

        public bool OpenedPickerFromEditor { get; set; }

        public bool OpenedAdjustColor { get; set; }

        public bool AdjustedColor { get; set; }

        public bool PickedSimilarColor { get; set; }

        public bool PickedColorFromHistory { get; set; }

        public bool RemovedColorFromHistory { get; set; }

        public bool CopiedColorToClipboard { get; set; }

        public TimeSpan Duration { get; set; }

        public PartA_PrivTags PartA_PrivTags => PartA_PrivTags.ProductAndServiceUsage;
    }
}
