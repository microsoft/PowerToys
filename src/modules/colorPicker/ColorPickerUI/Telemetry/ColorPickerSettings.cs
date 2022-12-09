// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Diagnostics.Tracing;
using Microsoft.PowerToys.Telemetry;
using Microsoft.PowerToys.Telemetry.Events;

namespace ColorPicker.Telemetry
{
    [EventData]
    public class ColorPickerSettings : EventBase, IEvent
    {
        public ColorPickerSettings(IDictionary<string, KeyValuePair<bool, string>> editorFormats)
        {
            EditorFormats = editorFormats;
            EventName = "ColorPicker_Settings";
        }

        public string ActivationShortcut { get; set; }

        public string ActivationBehaviour { get; set; }

        public string ColorFormatForClipboard { get; set; }

        public bool ShowColorName { get; set; }

        public IDictionary<string, KeyValuePair<bool, string>> EditorFormats { get; }

        public PartA_PrivTags PartA_PrivTags => PartA_PrivTags.ProductAndServiceUsage;
    }
}
