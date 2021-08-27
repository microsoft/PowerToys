// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.Text;
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

        public Device(string id, int dpi, Rect bounds, Rect workArea)
        {
            Id = id;
            Dpi = dpi;
            WorkAreaRect = workArea;
            UnscaledBounds = bounds;
            ScaledBounds = bounds;
        }

        public Device(Rect bounds, Rect workArea)
        {
            WorkAreaRect = workArea;
            UnscaledBounds = bounds;
            ScaledBounds = bounds;
        }

        public void Scale(double scaleFactor)
        {
            WorkAreaRect = new Rect(Math.Round(WorkAreaRect.X * scaleFactor), Math.Round(WorkAreaRect.Y * scaleFactor), Math.Round(WorkAreaRect.Width * scaleFactor), Math.Round(WorkAreaRect.Height * scaleFactor));
            ScaledBounds = new Rect(Math.Round(ScaledBounds.X * scaleFactor), Math.Round(ScaledBounds.Y * scaleFactor), Math.Round(ScaledBounds.Width * scaleFactor), Math.Round(ScaledBounds.Height * scaleFactor));
        }

        public double ScaleCoordinate(double coordinate)
        {
            float dpi = Dpi != 0 ? Dpi : 96f;
            double scaleFactor = 96f / dpi;

            return Math.Round(coordinate * scaleFactor);
        }

        public override string ToString()
        {
            var sb = new StringBuilder();

            sb.AppendFormat(CultureInfo.InvariantCulture, "ID: {0}{1}", Id, Environment.NewLine);
            sb.AppendFormat(CultureInfo.InvariantCulture, "DPI: {0}{1}", Dpi, Environment.NewLine);

            string workArea = string.Format(CultureInfo.InvariantCulture, "({0}, {1}, {2}, {3})", WorkAreaRect.X, WorkAreaRect.Y, WorkAreaRect.Width, WorkAreaRect.Height);
            string bounds = string.Format(CultureInfo.InvariantCulture, "({0}, {1}, {2}, {3})", UnscaledBounds.X, UnscaledBounds.Y, UnscaledBounds.Width, UnscaledBounds.Height);
            string scaledBounds = string.Format(CultureInfo.InvariantCulture, "({0}, {1}, {2}, {3})", ScaledBounds.X, ScaledBounds.Y, ScaledBounds.Width, ScaledBounds.Height);

            sb.AppendFormat(CultureInfo.InvariantCulture, "Work area: {0}{1}", workArea, Environment.NewLine);
            sb.AppendFormat(CultureInfo.InvariantCulture, "Unscaled bounds: {0}{1}", bounds, Environment.NewLine);
            sb.AppendFormat(CultureInfo.InvariantCulture, "Scaled bounds: {0}{1}", scaledBounds, Environment.NewLine);

            return sb.ToString();
        }
    }
}
