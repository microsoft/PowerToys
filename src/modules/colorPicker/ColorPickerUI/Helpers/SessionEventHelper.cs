// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using ColorPicker.Telemetry;
using Microsoft.PowerToys.Settings.UI.Library.Enumerations;
using Microsoft.PowerToys.Telemetry;

namespace ColorPicker.Helpers
{
    public static class SessionEventHelper
    {
        public static ColorPickerSession Event { get; private set; }

        public static void Start(ColorPickerActivationAction startedAs)
        {
            Event = new ColorPickerSession();
            Event.StartedAs = startedAs.ToString();
            _startTime = DateTime.Now;
        }

        public static void End()
        {
            if (_startTime == null)
            {
                Logger.LogError("Failed to send ColorPickerSessionEvent");
                return;
            }

            Event.Duration = DateTime.Now - _startTime.Value;
            _startTime = null;
            PowerToysTelemetry.Log.WriteEvent(Event);
        }

        private static DateTime? _startTime;
    }
}
