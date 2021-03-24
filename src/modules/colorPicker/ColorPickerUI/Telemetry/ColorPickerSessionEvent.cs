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

        public bool EditorOpenedAdjustColor { get; set; }

        public bool EditorAdjustedColor { get; set; }

        public bool EditorPickedSimilarColor { get; set; }

        public bool EditorPickedColorFromHistory { get; set; }

        public bool EditorRemovedColorFromHistory { get; set; }

        public bool EditorCopiedColorToClipboard { get; set; }

        public TimeSpan Duration { get; set; }

        public PartA_PrivTags PartA_PrivTags => PartA_PrivTags.ProductAndServiceUsage;
    }
}
