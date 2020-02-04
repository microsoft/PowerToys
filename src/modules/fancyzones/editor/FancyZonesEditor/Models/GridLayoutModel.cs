// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;

namespace FancyZonesEditor.Models
{
    // GridLayoutModel
    //  Grid-styled Layout Model, which specifies rows, columns, percentage sizes, and row/column spans
    public class GridLayoutModel : LayoutModel
    {
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

        public GridLayoutModel(string uuid, string name, LayoutType type, int rows, int cols, int[] rowPercents, int[] colsPercents, int[,] cellChildMap)
            : base(uuid, name, type)
        {
            _rows = rows;
            _cols = cols;
            RowPercents = rowPercents;
            ColumnPercents = colsPercents;
            CellChildMap = cellChildMap;
        }

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

        // PersistData
        // Implements the LayoutModel.PersistData abstract method
        protected override void PersistData()
        {
            FileStream outputStream = File.Open(Settings.AppliedZoneSetTmpFile, FileMode.Create);
            using (var writer = new Utf8JsonWriter(outputStream, options: default))
            {
                writer.WriteStartObject();
                writer.WriteString("uuid", "{" + Guid.ToString().ToUpper() + "}");
                writer.WriteString("name", Name);

                writer.WriteString("type", "grid");

                writer.WriteStartObject("info");

                writer.WriteNumber("rows", Rows);
                writer.WriteNumber("columns", Columns);

                writer.WriteStartArray("rows-percentage");
                for (int row = 0; row < Rows; row++)
                {
                    writer.WriteNumberValue(RowPercents[row]);
                }

                writer.WriteEndArray();

                writer.WriteStartArray("columns-percentage");
                for (int col = 0; col < Columns; col++)
                {
                    writer.WriteNumberValue(ColumnPercents[col]);
                }

                writer.WriteEndArray();

                writer.WriteStartArray("cell-child-map");
                for (int row = 0; row < Rows; row++)
                {
                    writer.WriteStartArray();
                    for (int col = 0; col < Columns; col++)
                    {
                        writer.WriteNumberValue(CellChildMap[row, col]);
                    }

                    writer.WriteEndArray();
                }

                writer.WriteEndArray();

                // end info object
                writer.WriteEndObject();

                // end root object
                writer.WriteEndObject();
                writer.Flush();
            }

            outputStream.Close();
        }
    }
}
