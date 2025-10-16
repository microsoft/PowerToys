// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using PowerDisplay.Core.Models;

namespace PowerDisplay.Core.Interfaces
{
    /// <summary>
    /// Monitor status changed event arguments
    /// </summary>
    public class MonitorStatusChangedEventArgs : EventArgs
    {
        public Monitor Monitor { get; }

        public int? OldBrightness { get; }

        public int NewBrightness { get; }

        public bool? OldAvailability { get; }

        public bool NewAvailability { get; }

        public string Message { get; }

        public ChangeType Type { get; }

        public enum ChangeType
        {
            Brightness,
            Contrast,
            Volume,
            ColorTemperature,
            Availability,
            General
        }

        public MonitorStatusChangedEventArgs(
            Monitor monitor,
            int? oldBrightness,
            int newBrightness,
            bool? oldAvailability,
            bool newAvailability)
        {
            Monitor = monitor;
            OldBrightness = oldBrightness;
            NewBrightness = newBrightness;
            OldAvailability = oldAvailability;
            NewAvailability = newAvailability;
            Message = $"Brightness changed from {oldBrightness} to {newBrightness}";
            Type = ChangeType.Brightness;
        }

        public MonitorStatusChangedEventArgs(
            Monitor monitor,
            string message,
            ChangeType changeType)
        {
            Monitor = monitor;
            Message = message;
            Type = changeType;
            
            // Set defaults for compatibility
            OldBrightness = null;
            NewBrightness = monitor.CurrentBrightness;
            OldAvailability = null;
            NewAvailability = monitor.IsAvailable;
        }
    }
}