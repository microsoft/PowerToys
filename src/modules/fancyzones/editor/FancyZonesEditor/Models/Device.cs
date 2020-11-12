// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Windows;

namespace FancyZonesEditor.Utils
{
    public class Device
    {
        public string Id { get; set; }

        public Rect UnscaledBounds { get; private set; }

        public Rect ScaledBounds { get; private set; }

        public Rect WorkAreaRect { get; private set; }

        public int Dpi { get; set; }

        public bool Primary { get; private set; }

        public Device(string id, int dpi, Rect bounds, Rect workArea, bool primary)
        {
            Id = id;
            Dpi = dpi;
            WorkAreaRect = workArea;
            UnscaledBounds = bounds;
            ScaledBounds = bounds;
            Primary = primary;
        }

        public Device(Rect bounds, Rect workArea, bool primary)
        {
            WorkAreaRect = workArea;
            UnscaledBounds = bounds;
            ScaledBounds = bounds;
            Primary = primary;
        }

        public void Scale(double scaleFactor)
        {
            WorkAreaRect = new Rect(Math.Round(WorkAreaRect.X * scaleFactor), Math.Round(WorkAreaRect.Y * scaleFactor), Math.Round(WorkAreaRect.Width * scaleFactor), Math.Round(WorkAreaRect.Height * scaleFactor));
            ScaledBounds = new Rect(Math.Round(ScaledBounds.X * scaleFactor), Math.Round(ScaledBounds.Y * scaleFactor), Math.Round(ScaledBounds.Width * scaleFactor), Math.Round(ScaledBounds.Height * scaleFactor));
        }
    }
}
