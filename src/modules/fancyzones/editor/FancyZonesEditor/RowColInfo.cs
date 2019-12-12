// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace FancyZonesEditor
{
    public class RowColInfo
    {
        public RowColInfo(int percent)
        {
            Percent = percent;
        }

        public RowColInfo(int index, int count)
        {
            Percent = (c_multiplier / count) + ((index == 0) ? (c_multiplier % count) : 0);
        }

        private const int c_multiplier = 10000;

        public double SetExtent(double start, double totalExtent)
        {
            Start = start;
            Extent = totalExtent * Percent / c_multiplier;
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

        public int Percent;
        public double Extent;
        public double Start;
        public double End;
    }
}
