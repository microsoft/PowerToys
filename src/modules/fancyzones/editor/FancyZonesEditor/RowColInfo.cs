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

        public RowColInfo(RowColInfo other)
        {
            Percent = other.Percent;
            Extent = other.Extent;
            Start = other.Start;
            End = other.End;
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

        public void RecalculatePercent(double newTotalExtent)
        {
            Percent = (int)(Extent * _multiplier / newTotalExtent);
        }

        public RowColInfo[] Split(double offset, double space)
        {
            RowColInfo[] info = new RowColInfo[2];

            double totalExtent = Extent * _multiplier / Percent;
            totalExtent -= space;

            int percent0 = (int)(offset * _multiplier / totalExtent);
            int percent1 = (int)((Extent - space - offset) * _multiplier / totalExtent);

            info[0] = new RowColInfo(percent0);
            info[1] = new RowColInfo(percent1);

            return info;
        }
    }
}
