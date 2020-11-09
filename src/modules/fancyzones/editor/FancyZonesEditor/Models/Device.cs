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

        public Rect Bounds { get; private set; }

        public Rect WorkAreaRect { get; private set; }

        public int Dpi { get; set; }

        public bool Primary { get; private set; }

        public Device(string id, int dpi, Rect bounds, Rect workArea, bool primary)
        {
            Id = id;
            Dpi = dpi;
            WorkAreaRect = workArea;
            Bounds = bounds;
            Primary = primary;
        }

        public Device(Rect bounds, Rect workArea, bool primary)
        {
            WorkAreaRect = workArea;
            Bounds = bounds;
            Primary = primary;
        }

        public void ScaleWorkArea(double scaleFactor)
        {
            WorkAreaRect = new Rect(WorkAreaRect.X * scaleFactor, WorkAreaRect.Y * scaleFactor, WorkAreaRect.Width * scaleFactor, WorkAreaRect.Height * scaleFactor);
        }
    }
}
