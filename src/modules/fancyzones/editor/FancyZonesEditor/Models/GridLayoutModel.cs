// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace FancyZonesEditor.Models
{
    // GridLayoutModel
    //  Grid-styled Layout Model, which specifies rows, columns, percentage sizes, and row/column spans
    public class GridLayoutModel : LayoutModel
    {
        public GridLayoutModel()
            : base()
        {
        }

        public GridLayoutModel(string name)
            : base(name)
        {
        }

        public GridLayoutModel(string name, ushort id)
            : base(name, id)
        {
        }

        public GridLayoutModel(ushort version, string name, ushort id, byte[] data)
            : base(name, id)
        {
            if (version == c_latestVersion)
            {
                Reload(data);
            }
        }

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
                    FirePropertyChanged("Rows");
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
                    FirePropertyChanged("Columns");
                }
            }
        }

        private int _cols = 1;

        // CellChildMap - represents which "children" belong in which grid cells;
        //  shows spanning children by the same index appearing in adjacent cells
        //  TODO: ideally no setter here - this means moving logic like "split" over to model
        public int[,] CellChildMap { get; set; }

        // RowPercents - represents the %age height of each row in the grid
        public int[] RowPercents { get; set; }

        // ColumnPercents - represents the %age width of each column in the grid
        public int[] ColumnPercents { get; set; }

        // FreeZones (not persisted) - used to keep track of child indices that are no longer in use in the CellChildMap,
        //  making them candidates for re-use when it's needed to add another child
        //  TODO: do I need FreeZones on the data model?  - I think I do
        public IList<int> FreeZones { get; } = new List<int>();

        public void Reload(byte[] data)
        {
            // Skip version (2 bytes), id (2 bytes), and type (1 bytes)
            int i = 5;

            Rows = data[i++];
            Columns = data[i++];

            RowPercents = new int[Rows];
            for (int row = 0; row < Rows; row++)
            {
                RowPercents[row] = (data[i++] * 256) + data[i++];
            }

            ColumnPercents = new int[Columns];
            for (int col = 0; col < Columns; col++)
            {
                ColumnPercents[col] = (data[i++] * 256) + data[i++];
            }

            CellChildMap = new int[Rows, Columns];
            for (int row = 0; row < Rows; row++)
            {
                for (int col = 0; col < Columns; col++)
                {
                    CellChildMap[row, col] = data[i++];
                }
            }
        }

        // Clone
        //  Implements the LayoutModel.Clone abstract method
        //  Clones the data from this GridLayoutModel to a new GridLayoutModel
        public override LayoutModel Clone()
        {
            GridLayoutModel layout = new GridLayoutModel(Name);
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

            int[] rowPercents = new int[rows];
            for (int row = 0; row < rows; row++)
            {
                rowPercents[row] = RowPercents[row];
            }

            layout.RowPercents = rowPercents;

            int[] colPercents = new int[cols];
            for (int col = 0; col < cols; col++)
            {
                colPercents[col] = ColumnPercents[col];
            }

            layout.ColumnPercents = colPercents;

            return layout;
        }

        // GetPersistData
        //  Implements the LayoutModel.GetPersistData abstract method
        //  Returns the state of this GridLayoutModel in persisted format
        protected override byte[] GetPersistData()
        {
            int rows = Rows;
            int cols = Columns;

            int[,] cellChildMap;

            if (FreeZones.Count == 0)
            {
                // no unused indices -- so we can just use the _cellChildMap as is
                cellChildMap = CellChildMap;
            }
            else
            {
                // compress cellChildMap to not have gaps for unused child indices;
                List<int> mapping = new List<int>();

                cellChildMap = new int[rows, cols];

                for (int row = 0; row < rows; row++)
                {
                    for (int col = 0; col < cols; col++)
                    {
                        int source = CellChildMap[row, col];

                        int index = mapping.IndexOf(source);
                        if (index == -1)
                        {
                            index = mapping.Count;
                            mapping.Add(source);
                        }

                        cellChildMap[row, col] = index;
                    }
                }
            }

            byte[] data = new byte[7 + (Rows * 2) + (Columns * 2) + (Rows * Columns)];

            int i = 0;

            // Common persisted values between all layout types
            data[i++] = (byte)(c_latestVersion / 256);
            data[i++] = (byte)(c_latestVersion % 256);
            data[i++] = 0; // LayoutModelType: 0 == GridLayoutModel
            data[i++] = (byte)(Id / 256);
            data[i++] = (byte)(Id % 256);

            // End common
            data[i++] = (byte)Rows;
            data[i++] = (byte)Columns;

            for (int row = 0; row < Rows; row++)
            {
                int rowPercent = RowPercents[row];
                data[i++] = (byte)(rowPercent / 256);
                data[i++] = (byte)(rowPercent % 256);
            }

            for (int col = 0; col < Columns; col++)
            {
                int colPercent = ColumnPercents[col];
                data[i++] = (byte)(colPercent / 256);
                data[i++] = (byte)(colPercent % 256);
            }

            for (int row = 0; row < Rows; row++)
            {
                for (int col = 0; col < Columns; col++)
                {
                    data[i++] = (byte)cellChildMap[row, col];
                }
            }

            return data;
        }

        private static ushort c_latestVersion = 0;
    }
}
