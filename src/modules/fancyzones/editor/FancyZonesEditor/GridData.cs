using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;

using FancyZonesEditor.Models;
using ManagedCommon;

namespace FancyZonesEditor
{
    /// <summary>
    /// Represents the data for a grid layout in the FancyZones editor.
    /// </summary>
    public class GridData
    {
        /// <summary>
        /// Calculates the prefix sum of a list of integers.
        /// </summary>
        /// <param name="list">The list of integers.</param>
        /// <returns>A list containing the prefix sums.</returns>
        public static List<int> PrefixSum(List<int> list)
        {
            var result = new List<int>(list.Count + 1);
            result.Add(0);

            int sum = 0;
            for (int i = 0; i < list.Count; i++)
            {
                sum += list[i];
                result.Add(sum);
            }

            return result;
        }

        /// <summary>
        /// Calculates the adjacent differences of a list of integers.
        /// </summary>
        /// <param name="list">The list of integers.</param>
        /// <returns>A list containing the adjacent differences.</returns>
        private static List<int> AdjacentDifference(List<int> list)
        {
            if (list.Count <= 1)
            {
                return new List<int>();
            }

            var result = new List<int>(list.Count - 1);

            for (int i = 0; i < list.Count - 1; i++)
            {
                result.Add(list[i + 1] - list[i]);
            }

            return result;
        }

        /// <summary>
        /// Returns a list of unique elements from the given list, preserving the order.
        /// </summary>
        /// <param name="list">The list of integers.</param>
        /// <returns>A list containing unique elements.</returns>
        private static List<int> Unique(List<int> list)
        {
            var result = new List<int>();

            if (list.Count == 0)
            {
                return result;
            }

            int last = list[0];
            result.Add(last);

            for (int i = 1; i < list.Count; i++)
            {
                if (list[i] != last)
                {
                    last = list[i];
                    result.Add(last);
                }
            }

            return result;
        }

        /// <summary>
        /// Represents a zone in the grid layout.
        /// </summary>
        public struct Zone
        {
            public int Index { get; set; }
            public int Left { get; set; }
            public int Top { get; set; }
            public int Right { get; set; }
            public int Bottom { get; set; }
        }

        /// <summary>
        /// Represents a resizer in the grid layout.
        /// </summary>
        public struct Resizer
        {
            public Orientation Orientation { get; set; }
            public List<int> NegativeSideIndices { get; set; }
            public List<int> PositiveSideIndices { get; set; }
        }

        private List<Zone> _zones;
        private List<Resizer> _resizers;

        public int MinZoneWidth { get; set; }
        public int MinZoneHeight { get; set; }

        private GridLayoutModel _model;

        /// <summary>
        /// The sum of row/column percents should be equal to this number.
        /// </summary>
        public const int Multiplier = 10000;

        /// <summary>
        /// Converts the grid layout model to a list of zones.
        /// </summary>
        /// <param name="model">The grid layout model.</param>
        private void ModelToZones(GridLayoutModel model)
        {
            int rows = model.Rows;
            int cols = model.Columns;

            int zoneCount = 0;

            for (int row = 0; row < rows; row++)
            {
                for (int col = 0; col < cols; col++)
                {
                    zoneCount = Math.Max(zoneCount, model.CellChildMap[row, col]);
                }
            }

            zoneCount++;

            if (zoneCount > rows * cols)
            {
                return;
            }

            var indexCount = Enumerable.Repeat(0, zoneCount).ToList();
            var indexRowLow = Enumerable.Repeat(int.MaxValue, zoneCount).ToList();
            var indexRowHigh = Enumerable.Repeat(0, zoneCount).ToList();
            var indexColLow = Enumerable.Repeat(int.MaxValue, zoneCount).ToList();
            var indexColHigh = Enumerable.Repeat(0, zoneCount).ToList();

            for (int row = 0; row < rows; row++)
            {
                for (int col = 0; col < cols; col++)
                {
                    int index = model.CellChildMap[row, col];
                    indexCount[index]++;
                    indexRowLow[index] = Math.Min(indexRowLow[index], row);
                    indexColLow[index] = Math.Min(indexColLow[index], col);
                    indexRowHigh[index] = Math.Max(indexRowHigh[index], row);
                    indexColHigh[index] = Math.Max(indexColHigh[index], col);
                }
            }

            for (int index = 0; index < zoneCount; index++)
            {
                if (indexCount[index] == 0)
                {
                    return;
                }

                if (indexCount[index] != (indexRowHigh[index] - indexRowLow[index] + 1) * (indexColHigh[index] - indexColLow[index] + 1))
                {
                    return;
                }
            }

            if (model.RowPercents.Count != model.Rows || model.ColumnPercents.Count != model.Columns || model.RowPercents.Exists((x) => (x < 1)) || model.ColumnPercents.Exists((x) => (x < 1)))
            {
                return;
            }

            var rowPrefixSum = PrefixSum(model.RowPercents);
            var colPrefixSum = PrefixSum(model.ColumnPercents);

            if (rowPrefixSum[rows] != Multiplier || colPrefixSum[cols] != Multiplier)
            {
                return;
            }

            _zones = new List<Zone>(zoneCount);

            for (int index = 0; index < zoneCount; index++)
            {
                _zones.Add(new Zone
                {
                    Index = index,
                    Left = colPrefixSum[indexColLow[index]],
                    Right = colPrefixSum[indexColHigh[index] + 1],
                    Top = rowPrefixSum[indexRowLow[index]],
                    Bottom = rowPrefixSum[indexRowHigh[index] + 1],
                });
            }
        }

        /// <summary>
        /// Converts the grid layout model to a list of resizers.
        /// </summary>
        /// <param name="model">The grid layout model.</param>
        private void ModelToResizers(GridLayoutModel model)
        {
            var grid = model.CellChildMap;

            int rows = model.Rows;
            int cols = model.Columns;

            _resizers = new List<Resizer>();

            // Horizontal resizers
            for (int row = 1; row < rows; row++)
            {
                for (int startCol = 0; startCol < cols;)
                {
                    if (grid[row - 1, startCol] != grid[row, startCol])
                    {
                        int endCol = startCol;
                        while (endCol + 1 < cols && grid[row - 1, endCol + 1] != grid[row, endCol + 1])
                        {
                            endCol++;
                        }

                        var resizer = default(Resizer);
                        resizer.Orientation = Orientation.Horizontal;
                        var positive = new List<int>();
                        var negative = new List<int>();

                        for (int col = startCol; col <= endCol; col++)
                        {
                            negative.Add(grid[row - 1, col]);
                            positive.Add(grid[row, col]);
                        }

                        resizer.PositiveSideIndices = Unique(positive);
                        resizer.NegativeSideIndices = Unique(negative);

                        _resizers.Add(resizer);

                        startCol = endCol + 1;
                    }
                    else
                    {
                        startCol++;
                    }
                }
            }

            // Vertical resizers
            for (int col = 1; col < cols; col++)
            {
                for (int startRow = 0; startRow < rows;)
                {
                    if (grid[startRow, col - 1] != grid[startRow, col])
                    {
                        int endRow = startRow;
                        while (endRow + 1 < rows && grid[endRow + 1, col - 1] != grid[endRow + 1, col])
                        {
                            endRow++;
                        }

                        var resizer = default(Resizer);
                        resizer.Orientation = Orientation.Vertical;
                        var positive = new List<int>();
                        var negative = new List<int>();

                        for (int row = startRow; row <= endRow; row++)
                        {
                            negative.Add(grid[row, col - 1]);
                            positive.Add(grid[row, col]);
                        }

                        resizer.PositiveSideIndices = Unique(positive);
                        resizer.NegativeSideIndices = Unique(negative);

                        _resizers.Add(resizer);

                        startRow = endRow + 1;
                    }
                    else
                    {
                        startRow++;
                    }
                }
            }
        }

        /// <summary>
        /// Converts the grid layout model to zones and resizers.
        /// </summary>
        /// <param name="model">The grid layout model.</param>
        private void FromModel(GridLayoutModel model)
        {
            ModelToZones(model);
            ModelToResizers(model);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="GridData"/> class.
        /// </summary>
        /// <param name="model">The grid layout model.</param>
        public GridData(GridLayoutModel model)
        {
            _model = model;

            MinZoneWidth = 1;
            MinZoneHeight = 1;
            FromModel(model);
        }

        /// <summary>
        /// Gets the list of zones in the grid layout.
        /// </summary>
        public IReadOnlyList<Zone> Zones
        {
            get { return _zones; }
        }

        /// <summary>
        /// Gets the list of resizers in the grid layout.
        /// </summary>
        public IReadOnlyList<Resizer> Resizers
        {
            get { return _resizers; }
        }

        /// <summary>
        /// Converts the list of zones to the grid layout model.
        /// </summary>
        /// <param name="model">The grid layout model.</param>
        private void ZonesToModel(GridLayoutModel model)
        {
            var xCoords = _zones.Select((zone) => zone.Right).Concat(_zones.Select((zone) => zone.Left)).Distinct().OrderBy((x) => x).ToList();
            var yCoords = _zones.Select((zone) => zone.Top).Concat(_zones.Select((zone) => zone.Bottom)).Distinct().OrderBy((x) => x).ToList();

            model.Rows = yCoords.Count - 1;
            model.Columns = xCoords.Count - 1;
            model.RowPercents = AdjacentDifference(yCoords);
            model.ColumnPercents = AdjacentDifference(xCoords);
            model.CellChildMap = new int[model.Rows, model.Columns];

            for (int index = 0; index < _zones.Count; index++)
            {
                Zone zone = _zones[index];
                int startRow = yCoords.IndexOf(zone.Top);
                int endRow = yCoords.IndexOf(zone.Bottom);
                int startCol = xCoords.IndexOf(zone.Left);
                int endCol = xCoords.IndexOf(zone.Right);

                for (int row = startRow; row < endRow; row++)
                {
                    for (int col = startCol; col < endCol; col++)
                    {
                        model.CellChildMap[row, col] = index;
                    }
                }
            }
        }

        /// <summary>
        /// Computes the closure of the given list of indices.
        /// </summary>
        /// <param name="indices">The list of indices.</param>
        /// <returns>A tuple containing the list of indices and the zone to replace the merged zones.</returns>
        private Tuple<List<int>, Zone> ComputeClosure(List<int> indices)
        {
            int left = int.MaxValue;
            int right = int.MinValue;
            int top = int.MaxValue;
            int bottom = int.MinValue;

            if (indices.Count == 0)
            {
                return new Tuple<List<int>, Zone>(new List<int>(), new Zone
                {
                    Index = -1,
                    Left = left,
                    Right = right,
                    Top = top,
                    Bottom = bottom,
                });
            }

            void Extend(Zone zone)
            {
                left = Math.Min(left, zone.Left);
                right = Math.Max(right, zone.Right);
                top = Math.Min(top, zone.Top);
                bottom = Math.Max(bottom, zone.Bottom);
            }

            foreach (Index index in indices)
            {
                Extend(_zones[index]);
            }

            bool possiblyBroken = true;
            while (possiblyBroken)
            {
                possiblyBroken = false;
                foreach (Zone zone in _zones)
                {
                    int area = (zone.Bottom - zone.Top) * (zone.Right - zone.Left);

                    int cutLeft = Math.Max(left, zone.Left);
                    int cutRight = Math.Min(right, zone.Right);
                    int cutTop = Math.Max(top, zone.Top);
                    int cutBottom = Math.Min(bottom, zone.Bottom);

                    int newArea = Math.Max(0, cutBottom - cutTop) * Math.Max(0, cutRight - cutLeft);

                    if (newArea != 0 && newArea != area)
                    {
                        Extend(zone);
                        possiblyBroken = true;
                    }
                }
            }

            var resultIndices = _zones.FindAll((zone) =>
            {
                bool inside = true;
                inside &= left <= zone.Left && zone.Right <= right;
                inside &= top <= zone.Top && zone.Bottom <= bottom;
                return inside;
            }).Select((zone) => zone.Index).ToList();

            return new Tuple<List<int>, Zone>(resultIndices, new Zone
            {
                Index = -1,
                Left = left,
                Right = right,
                Top = top,
                Bottom = bottom,
            });
        }

        /// <summary>
        /// Returns the list of indices in the closure of the given list of indices.
        /// </summary>
        /// <param name="indices">The list of indices.</param>
        /// <returns>The list of indices in the closure.</returns>
        public List<int> MergeClosureIndices(List<int> indices)
        {
            return ComputeClosure(indices).Item1;
        }

        /// <summary>
        /// Merges the zones with the given indices.
        /// </summary>
        /// <param name="indices">The list of indices.</param>
        public void DoMerge(List<int> indices)
        {
            Logger.LogTrace();

            if (indices.Count == 0)
            {
                return;
            }

            int lowestIndex = indices.Min();

            var closure = ComputeClosure(indices);
            var closureIndices = closure.Item1.ToHashSet();
            Zone closureZone = closure.Item2;

            _zones = _zones.FindAll((zone) => !closureIndices.Contains(zone.Index)).ToList();

            _zones.Insert(lowestIndex, closureZone);

            ZonesToModel(_model);
            FromModel(_model);
        }

        /// <summary>
        /// Determines whether the zone can be split at the given position.
        /// </summary>
        /// <param name="zoneIndex">The index of the zone.</param>
        /// <param name="position">The position to split.</param>
        /// <param name="orientation">The orientation of the split.</param>
        /// <returns>True if the zone can be split; otherwise, false.</returns>
        public bool CanSplit(int zoneIndex, int position, Orientation orientation)
        {
            Zone zone = _zones[zoneIndex];

            if (orientation == Orientation.Horizontal)
            {
                return zone.Top + MinZoneHeight <= position && position <= zone.Bottom - MinZoneHeight;
            }
            else
            {
                return zone.Left + MinZoneWidth <= position && position <= zone.Right - MinZoneWidth;
            }
        }

        /// <summary>
        /// Splits the zone at the given position.
        /// </summary>
        /// <param name="zoneIndex">The index of the zone.</param>
        /// <param name="position">The position to split.</param>
        /// <param name="orientation">The orientation of the split.</param>
        public void Split(int zoneIndex, int position, Orientation orientation)
        {
            Logger.LogTrace();
            if (!CanSplit(zoneIndex, position, orientation))
            {
                return;
            }

            Zone zone = _zones[zoneIndex];
            Zone zone1 = zone;
            Zone zone2 = zone;

            _zones.RemoveAt(zoneIndex);

            if (orientation == Orientation.Horizontal)
            {
                zone1.Bottom = position;
                zone2.Top = position;
            }
            else
            {
                zone1.Right = position;
                zone2.Left = position;
            }

            _zones.Insert(zoneIndex, zone1);
            _zones.Insert(zoneIndex + 1, zone2);

            ZonesToModel(_model);
            FromModel(_model);
        }

        /// <summary>
        /// Determines whether the resizer can be dragged by the given amount.
        /// </summary>
        /// <param name="resizerIndex">The index of the resizer.</param>
        /// <param name="delta">The amount to drag.</param>
        /// <returns>True if the resizer can be dragged; otherwise, false.</returns>
        public bool CanDrag(int resizerIndex, int delta)
        {
            var resizer = _resizers[resizerIndex];

            int GetSize(int zoneIndex)
            {
                Zone zone = _zones[zoneIndex];
                return resizer.Orientation == Orientation.Vertical ? zone.Right - zone.Left : zone.Bottom - zone.Top;
            }

            int minZoneSize = resizer.Orientation == Orientation.Vertical ? MinZoneWidth : MinZoneHeight;

            foreach (int zoneIndex in resizer.PositiveSideIndices)
            {
                if (GetSize(zoneIndex) - delta < minZoneSize)
                {
                    return false;
                }
            }

            foreach (int zoneIndex in resizer.NegativeSideIndices)
            {
                if (GetSize(zoneIndex) + delta < minZoneSize)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Drags the resizer by the given amount.
        /// </summary>
        /// <param name="resizerIndex">The index of the resizer.</param>
        /// <param name="delta">The amount to drag.</param>
        public void Drag(int resizerIndex, int delta)
        {
            Logger.LogTrace();

            if (!CanDrag(resizerIndex, delta))
            {
                return;
            }

            var resizer = _resizers[resizerIndex];

            foreach (int zoneIndex in resizer.PositiveSideIndices)
            {
                var zone = _zones[zoneIndex];

                if (resizer.Orientation == Orientation.Horizontal)
                {
                    zone.Top += delta;
                }
                else
                {
                    zone.Left += delta;
                }

                _zones[zoneIndex] = zone;
            }

            foreach (int zoneIndex in resizer.NegativeSideIndices)
            {
                var zone = _zones[zoneIndex];

                if (resizer.Orientation == Orientation.Horizontal)
                {
                    zone.Bottom += delta;
                }
                else
                {
                    zone.Right += delta;
                }

                _zones[zoneIndex] = zone;
            }

            ZonesToModel(_model);
            FromModel(_model);
        }
    }
}
