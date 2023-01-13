// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

namespace FancyZonesEditor.Models
{
    // GridLayoutModel
    //  Grid-styled Layout Model, which specifies rows, columns, percentage sizes, and row/column spans
    public class GridLayoutModel : LayoutModel
    {
        // Non-localizable strings
        public const string ModelTypeID = "grid";

        public const int GridMultiplier = 10000;

        // hard coded data for all the "Priority Grid" configurations that are unique to "Grid"
        private static readonly byte[][] _priorityData = new byte[][]
        {
            new byte[] { 0, 0, 0, 0, 0, 1, 1, 39, 16, 39, 16, 0 },
            new byte[] { 0, 0, 0, 0, 0, 1, 2, 39, 16, 26, 11, 13, 5, 0, 1 },
            new byte[] { 0, 0, 0, 0, 0, 1, 3, 39, 16, 9, 196, 19, 136, 9, 196, 0, 1, 2 },
            new byte[] { 0, 0, 0, 0, 0, 2, 3, 19, 136, 19, 136, 9, 196, 19, 136, 9, 196, 0, 1, 2, 0, 1, 3 },
            new byte[] { 0, 0, 0, 0, 0, 2, 3, 19, 136, 19, 136, 9, 196, 19, 136, 9, 196, 0, 1, 2, 3, 1, 4 },
            new byte[] { 0, 0, 0, 0, 0, 3, 3, 13, 5, 13, 6, 13, 5, 9, 196, 19, 136, 9, 196, 0, 1, 2, 0, 1, 3, 4, 1, 5 },
            new byte[] { 0, 0, 0, 0, 0, 3, 3, 13, 5, 13, 6, 13, 5, 9, 196, 19, 136, 9, 196, 0, 1, 2, 3, 1, 4, 5, 1, 6 },
            new byte[] { 0, 0, 0, 0, 0, 3, 4, 13, 5, 13, 6, 13, 5, 9, 196, 9, 196, 9, 196, 9, 196, 0, 1, 2, 3, 4, 1, 2, 5, 6, 1, 2, 7 },
            new byte[] { 0, 0, 0, 0, 0, 3, 4, 13, 5, 13, 6, 13, 5, 9, 196, 9, 196, 9, 196, 9, 196, 0, 1, 2, 3, 4, 1, 2, 5, 6, 1, 7, 8 },
            new byte[] { 0, 0, 0, 0, 0, 3, 4, 13, 5, 13, 6, 13, 5, 9, 196, 9, 196, 9, 196, 9, 196, 0, 1, 2, 3, 4, 1, 5, 6, 7, 1, 8, 9 },
            new byte[] { 0, 0, 0, 0, 0, 3, 4, 13, 5, 13, 6, 13, 5, 9, 196, 9, 196, 9, 196, 9, 196, 0, 1, 2, 3, 4, 1, 5, 6, 7, 8, 9, 10 },
        };

        // Rows - number of rows in the Grid
        public int Rows
        {
            get
            {
                return _rows;
            }

            set
            {
                if (_rows != value)
                {
                    _rows = value;
                }
            }
        }

        private int _rows = 1;

        // Columns - number of columns in the Grid
        public int Columns
        {
            get
            {
                return _cols;
            }

            set
            {
                if (_cols != value)
                {
                    _cols = value;
                }
            }
        }

        private int _cols = 1;

        // CellChildMap - represents which "children" belong in which grid cells;
        //  shows spanning children by the same index appearing in adjacent cells
        //  TODO: ideally no setter here - this means moving logic like "split" over to model
        public int[,] CellChildMap { get; set; }

        // RowPercents - represents the %age height of each row in the grid
        public List<int> RowPercents { get; set; } = new List<int>();

        // ColumnPercents - represents the %age width of each column in the grid
        public List<int> ColumnPercents { get; set; } = new List<int>();

        // ShowSpacing - flag if free space between cells should be presented
        public bool ShowSpacing
        {
            get
            {
                return _showSpacing;
            }

            set
            {
                if (value != _showSpacing)
                {
                    _showSpacing = value;
                    FirePropertyChanged(nameof(ShowSpacing));
                }
            }
        }

        private bool _showSpacing = LayoutSettings.DefaultShowSpacing;

        // Spacing - free space between cells
        public int Spacing
        {
            get
            {
                return _spacing;
            }

            set
            {
                if (value != _spacing)
                {
                    _spacing = value;
                    FirePropertyChanged(nameof(Spacing));
                }
            }
        }

        public int SpacingMinimum
        {
            get { return -10; }
        }

        public int SpacingMaximum
        {
            get { return 1000; }
        }

        private int _spacing = LayoutSettings.DefaultSpacing;

        public GridLayoutModel()
            : base()
        {
        }

        public GridLayoutModel(string name)
            : base(name)
        {
        }

        public GridLayoutModel(string name, LayoutType type)
            : base(name, type)
        {
        }

        public GridLayoutModel(string uuid, string name, LayoutType type, int rows, int cols, List<int> rowPercents, List<int> colsPercents, int[,] cellChildMap)
            : base(uuid, name, type)
        {
            _rows = rows;
            _cols = cols;
            RowPercents = rowPercents;
            ColumnPercents = colsPercents;
            CellChildMap = cellChildMap;
        }

        public GridLayoutModel(GridLayoutModel other)
            : base(other)
        {
            _rows = other._rows;
            _cols = other._cols;
            _showSpacing = other._showSpacing;
            _spacing = other._spacing;

            CellChildMap = new int[_rows, _cols];
            for (int row = 0; row < _rows; row++)
            {
                for (int col = 0; col < _cols; col++)
                {
                    CellChildMap[row, col] = other.CellChildMap[row, col];
                }
            }

            for (int row = 0; row < _rows; row++)
            {
                RowPercents.Add(other.RowPercents[row]);
            }

            for (int col = 0; col < _cols; col++)
            {
                ColumnPercents.Add(other.ColumnPercents[col]);
            }
        }

        public bool IsModelValid()
        {
            // Check if rows and columns are valid
            if (Rows <= 0 || Columns <= 0)
            {
                return false;
            }

            // Check if percentage is valid.
            if (RowPercents.Count != Rows || ColumnPercents.Count != Columns || RowPercents.Exists((x) => (x < 1)) || ColumnPercents.Exists((x) => (x < 1)))
            {
                return false;
            }

            // Check if cells map is valid
            if (CellChildMap.Length != Rows * Columns)
            {
                return false;
            }

            int zoneCount = 0;
            for (int row = 0; row < Rows; row++)
            {
                for (int col = 0; col < Columns; col++)
                {
                    zoneCount = Math.Max(zoneCount, CellChildMap[row, col]);
                }
            }

            zoneCount++;

            if (zoneCount > Rows * Columns)
            {
                return false;
            }

            var rowPrefixSum = GridData.PrefixSum(RowPercents);
            var colPrefixSum = GridData.PrefixSum(ColumnPercents);

            if (rowPrefixSum[Rows] != GridData.Multiplier || colPrefixSum[Columns] != GridData.Multiplier)
            {
                return false;
            }

            return true;
        }

        public void UpdatePreview()
        {
            FirePropertyChanged();
        }

        public void Reload(byte[] data)
        {
            // Skip version (2 bytes), id (2 bytes), and type (1 bytes)
            int i = 5;

            _rows = data[i++];
            _cols = data[i++];

            RowPercents = new List<int>(Rows);
            for (int row = 0; row < Rows; row++)
            {
                RowPercents.Add((data[i++] * 256) + data[i++]);
            }

            ColumnPercents = new List<int>(Columns);
            for (int col = 0; col < Columns; col++)
            {
                ColumnPercents.Add((data[i++] * 256) + data[i++]);
            }

            CellChildMap = new int[Rows, Columns];
            for (int row = 0; row < Rows; row++)
            {
                for (int col = 0; col < Columns; col++)
                {
                    CellChildMap[row, col] = data[i++];
                }
            }

            FirePropertyChanged();
        }

        // Clone
        //  Implements the LayoutModel.Clone abstract method
        //  Clones the data from this GridLayoutModel to a new GridLayoutModel
        public override LayoutModel Clone()
        {
            GridLayoutModel layout = new GridLayoutModel(Name);
            RestoreTo(layout);
            return layout;
        }

        public void RestoreTo(GridLayoutModel layout)
        {
            int rows = Rows;
            int cols = Columns;

            layout.Rows = rows;
            layout.Columns = cols;

            int[,] cellChildMap = new int[rows, cols];
            for (int row = 0; row < rows; row++)
            {
                for (int col = 0; col < cols; col++)
                {
                    cellChildMap[row, col] = CellChildMap[row, col];
                }
            }

            layout.CellChildMap = cellChildMap;

            List<int> rowPercents = new List<int>(rows);
            for (int row = 0; row < rows; row++)
            {
                rowPercents.Add(RowPercents[row]);
            }

            layout.RowPercents = rowPercents;

            List<int> colPercents = new List<int>(cols);
            for (int col = 0; col < cols; col++)
            {
                colPercents.Add(ColumnPercents[col]);
            }

            layout.ColumnPercents = colPercents;

            layout.ShowSpacing = ShowSpacing;
            layout.Spacing = Spacing;
            layout.SensitivityRadius = SensitivityRadius;

            layout.FirePropertyChanged();
        }

        // InitTemplateZones
        // Creates zones based on template zones count
        public override void InitTemplateZones()
        {
            switch (Type)
            {
                case LayoutType.Rows:
                    InitRows();
                    break;
                case LayoutType.Columns:
                    InitColumns();
                    break;
                case LayoutType.Grid:
                    InitGrid();
                    break;
                case LayoutType.PriorityGrid:
                    InitPriorityGrid();
                    break;
                case LayoutType.Custom:
                    InitColumns(); // Custom is initialized with columns
                    break;
            }

            FirePropertyChanged();
        }

        // PersistData
        // Implements the LayoutModel.PersistData abstract method
        protected override void PersistData()
        {
            AddCustomLayout(this);
        }

        private void InitRows()
        {
            CellChildMap = new int[TemplateZoneCount, 1];
            RowPercents = new List<int>(TemplateZoneCount);

            for (int i = 0; i < TemplateZoneCount; i++)
            {
                CellChildMap[i, 0] = i;

                // Note: This is NOT equal to _multiplier / ZoneCount and is done like this to make
                // the sum of all RowPercents exactly (_multiplier).
                RowPercents.Add(((GridMultiplier * (i + 1)) / TemplateZoneCount) - ((GridMultiplier * i) / TemplateZoneCount));
            }

            _rows = TemplateZoneCount;
        }

        private void InitColumns()
        {
            CellChildMap = new int[1, TemplateZoneCount];
            ColumnPercents = new List<int>(TemplateZoneCount);

            for (int i = 0; i < TemplateZoneCount; i++)
            {
                CellChildMap[0, i] = i;

                // Note: This is NOT equal to _multiplier / ZoneCount and is done like this to make
                // the sum of all RowPercents exactly (_multiplier).
                ColumnPercents.Add(((GridMultiplier * (i + 1)) / TemplateZoneCount) - ((GridMultiplier * i) / TemplateZoneCount));
            }

            _cols = TemplateZoneCount;
        }

        private void InitGrid()
        {
            int rows = 1;
            while (TemplateZoneCount / rows >= rows)
            {
                rows++;
            }

            rows--;
            int cols = TemplateZoneCount / rows;
            if (TemplateZoneCount % rows == 0)
            {
                // even grid
            }
            else
            {
                cols++;
            }

            RowPercents = new List<int>(rows);
            ColumnPercents = new List<int>(cols);
            CellChildMap = new int[rows, cols];

            // Note: The following are NOT equal to _multiplier divided by rows or columns and is
            // done like this to make the sum of all RowPercents exactly (_multiplier).
            for (int row = 0; row < rows; row++)
            {
                RowPercents.Add(((GridMultiplier * (row + 1)) / rows) - ((GridMultiplier * row) / rows));
            }

            for (int col = 0; col < cols; col++)
            {
                ColumnPercents.Add(((GridMultiplier * (col + 1)) / cols) - ((GridMultiplier * col) / cols));
            }

            int index = 0;
            for (int row = 0; row < rows; row++)
            {
                for (int col = 0; col < cols; col++)
                {
                    CellChildMap[row, col] = index++;
                    if (index == TemplateZoneCount)
                    {
                        index--;
                    }
                }
            }

            _rows = rows;
            _cols = cols;
        }

        private void InitPriorityGrid()
        {
            if (TemplateZoneCount <= _priorityData.Length)
            {
                Reload(_priorityData[TemplateZoneCount - 1]);
            }
            else
            {
                // same as grid;
                InitGrid();
            }
        }
    }
}
