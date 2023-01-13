// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics.Tracing;
using Microsoft.PowerToys.Telemetry;
using Microsoft.PowerToys.Telemetry.Events;

namespace ColorPicker.Telemetry
{
    [EventData]
    public class ColorPickerSession : EventBase, IEvent
    {
        public ColorPickerSession()
        {
            EventName = "ColorPicker_Session";
        }

        public string StartedAs { get; set; }

        public bool ZoomUsed { get; set; }

        public bool EditorOpened { get; set; }

        public bool EditorColorPickerOpened { get; set; }

        public bool EditorAdjustColorOpened { get; set; }

        public bool EditorColorAdjusted { get; set; }

        public bool EditorColorsExported { get; set; }

        public bool EditorSimilarColorPicked { get; set; }

        public bool EditorHistoryColorPicked { get; set; }

        public bool EditorHistoryColorRemoved { get; set; }

        public bool EditorColorCopiedToClipboard { get; set; }

        public int Duration { get; set; }

        public PartA_PrivTags PartA_PrivTags => PartA_PrivTags.ProductAndServiceUsage;
    }
}
