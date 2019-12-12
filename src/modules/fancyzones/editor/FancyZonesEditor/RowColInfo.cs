// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace FancyZonesEditor
{
    public class RowColInfo
    {
        private const int _multiplier = 10000;

        public double Extent { get; set; }

        public double Start { get; set; }

        public double End { get; set; }

        public int Percent { get; set; }

        public RowColInfo(int percent)
        {
            Percent = percent;
        }

        public RowColInfo(int index, int count)
        {
            Percent = (_multiplier / count) + ((index == 0) ? (_multiplier % count) : 0);
        }

        public double Recalculate(double start, double totalExtent)
        {
            Start = start;
            Extent = totalExtent * Percent / _multiplier;
            End = Start + Extent;
            return Extent;
        }

        public RowColInfo[] Split(double offset)
        {
            RowColInfo[] info = new RowColInfo[2];

            int newPercent = (int)(Percent * offset / Extent);
            info[0] = new RowColInfo(newPercent);
            info[1] = new RowColInfo(Percent - newPercent);
            return info;
        }
    }
}
