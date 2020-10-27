// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Windows;

namespace FancyZonesEditor.Utils
{
    public class WorkAreaData
    {
        public string Id { get; private set; }

        public Rect Bounds { get; private set; }

        public Rect WorkAreaRect { get; private set; }

        public int Dpi { get; private set; }

        public WorkAreaData(string id, int dpi, Rect monitor, Rect workArea)
        {
            Id = id;
            Dpi = dpi;

            WorkAreaRect = workArea;
            Bounds = monitor;
        }

        public double ConvertDpi(double value)
        {
            return Math.Round(value * WorkArea.SmallestUsedDPI / Dpi);
        }

        private double ConvertDefaultDpi(double value)
        {
            return Math.Round(value * WorkArea.DefaultDPI / WorkArea.SmallestUsedDPI);
        }
    }
}
