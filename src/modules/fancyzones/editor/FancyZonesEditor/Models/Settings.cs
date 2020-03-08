// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Windows;
using FancyZonesEditor.Models;

namespace FancyZonesEditor
{
    // Settings
    //  These are the configuration settings used by the rest of the editor
    //  Other UIs in the editor will subscribe to change events on the properties to stay up to date as these properties change
    public class Settings : INotifyPropertyChanged
    {
        private enum CmdArgs
        {
            MonitorHandle = 1,
            X_Y_Width_Height,
            ResolutionKey,
            ActiveZoneSetTmpFile,
            AppliedZoneSetTmpFile,
            CustomZoneSetsTmpFile,
        }

        private static CanvasLayoutModel _blankCustomModel;
        private readonly CanvasLayoutModel _focusModel;
        private readonly GridLayoutModel _rowsModel;
        private readonly GridLayoutModel _columnsModel;
        private readonly GridLayoutModel _gridModel;
        private readonly GridLayoutModel _priorityGridModel;

        public const ushort _focusModelId = 0xFFFF;
        public const ushort _rowsModelId = 0xFFFE;
        public const ushort _columnsModelId = 0xFFFD;
        public const ushort _gridModelId = 0xFFFC;
        public const ushort _priorityGridModelId = 0xFFFB;
        public const ushort _blankCustomModelId = 0xFFFA;
        public const ushort _lastPrefinedId = _blankCustomModelId;

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

        private const int _multiplier = 10000;

        public bool IsCustomLayoutActive
        {
            get
            {
                foreach (LayoutModel model in CustomModels)
                {
                    if (model.IsSelected)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        public Settings()
        {
            ParseCommandLineArgs();

            // Initialize the five default layout models: Focus, Columns, Rows, Grid, and PriorityGrid
            DefaultModels = new List<LayoutModel>(5);
            _focusModel = new CanvasLayoutModel("Focus", LayoutType.Focus, (int)_workArea.Width, (int)_workArea.Height);
            DefaultModels.Add(_focusModel);

            _columnsModel = new GridLayoutModel("Columns", LayoutType.Columns)
            {
                Rows = 1,
                RowPercents = new int[1] { _multiplier },
            };
            DefaultModels.Add(_columnsModel);

            _rowsModel = new GridLayoutModel("Rows", LayoutType.Rows)
            {
                Columns = 1,
                ColumnPercents = new int[1] { _multiplier },
            };
            DefaultModels.Add(_rowsModel);

            _gridModel = new GridLayoutModel("Grid", LayoutType.Grid);
            DefaultModels.Add(_gridModel);

            _priorityGridModel = new GridLayoutModel("Priority Grid", LayoutType.PriorityGrid);
            DefaultModels.Add(_priorityGridModel);

            _blankCustomModel = new CanvasLayoutModel("Create new custom", LayoutType.Blank, (int)_workArea.Width, (int)_workArea.Height);

            UpdateLayoutModels();
        }

        // ZoneCount - number of zones selected in the picker window
        public int ZoneCount
        {
            get
            {
                return _zoneCount;
            }

            set
            {
                if (_zoneCount != value)
                {
                    _zoneCount = value;
                    UpdateLayoutModels();
                    FirePropertyChanged("ZoneCount");
                }
            }
        }

        private int _zoneCount;

        // Spacing - how much space in between zones of the grid do you want
        public int Spacing
        {
            get
            {
                return _spacing;
            }

            set
            {
                if (_spacing != value)
                {
                    _spacing = value;
                    FirePropertyChanged("Spacing");
                }
            }
        }

        private int _spacing;

        // ShowSpacing - is the Spacing value used or ignored?
        public bool ShowSpacing
        {
            get
            {
                return _showSpacing;
            }

            set
            {
                if (_showSpacing != value)
                {
                    _showSpacing = value;
                    FirePropertyChanged("ShowSpacing");
                }
            }
        }

        private bool _showSpacing;

        // IsShiftKeyPressed - is the shift key currently being held down
        public bool IsShiftKeyPressed
        {
            get
            {
                return _isShiftKeyPressed;
            }

            set
            {
                if (_isShiftKeyPressed != value)
                {
                    _isShiftKeyPressed = value;
                    FirePropertyChanged("IsShiftKeyPressed");
                }
            }
        }

        private bool _isShiftKeyPressed;

        // IsCtrlKeyPressed - is the ctrl key currently being held down
        public bool IsCtrlKeyPressed
        {
            get
            {
                return _isCtrlKeyPressed;
            }

            set
            {
                if (_isCtrlKeyPressed != value)
                {
                    _isCtrlKeyPressed = value;
                    FirePropertyChanged("IsCtrlKeyPressed");
                }
            }
        }

        private bool _isCtrlKeyPressed;

        public Rect WorkArea
        {
            get { return _workArea; }
        }

        private Rect _workArea;

        public static uint Monitor { get; private set; }

        public static string UniqueKey { get; private set; }

        public static string ActiveZoneSetUUid { get; private set; }

        public static LayoutType ActiveZoneSetLayoutType { get; private set; }

        public static string ActiveZoneSetTmpFile
        {
            get { return _activeZoneSetTmpFile; }
        }

        private static string _activeZoneSetTmpFile;

        public static string AppliedZoneSetTmpFile
        {
            get { return _appliedZoneSetTmpFile; }
        }

        private static string _appliedZoneSetTmpFile;

        public static string CustomZoneSetsTmpFile
        {
            get { return _customZoneSetsTmpFile; }
        }

        private static string _customZoneSetsTmpFile;

        public static string WorkAreaKey { get; private set; }

        // UpdateLayoutModels
        //  Update the five default layouts based on the new ZoneCount
        private void UpdateLayoutModels()
        {
            // Update the "Focus" Default Layout
            _focusModel.Zones.Clear();

            // Sanity check for imported settings that may have invalid data
            if (ZoneCount < 1)
            {
                ZoneCount = 3;
            }

            Int32Rect focusZoneRect = new Int32Rect((int)(_focusModel.ReferenceWidth * 0.1), (int)(_focusModel.ReferenceHeight * 0.1), (int)(_focusModel.ReferenceWidth * 0.6), (int)(_focusModel.ReferenceHeight * 0.6));
            int focusRectXIncrement = (ZoneCount <= 1) ? 0 : (int)(_focusModel.ReferenceWidth * 0.2) / (ZoneCount - 1);
            int focusRectYIncrement = (ZoneCount <= 1) ? 0 : (int)(_focusModel.ReferenceHeight * 0.2) / (ZoneCount - 1);

            for (int i = 0; i < ZoneCount; i++)
            {
                _focusModel.Zones.Add(focusZoneRect);
                focusZoneRect.X += focusRectXIncrement;
                focusZoneRect.Y += focusRectYIncrement;
            }

            // Update the "Rows" and "Columns" Default Layouts
            // They can share their model, just transposed
            _rowsModel.CellChildMap = new int[ZoneCount, 1];
            _columnsModel.CellChildMap = new int[1, ZoneCount];
            _rowsModel.Rows = _columnsModel.Columns = ZoneCount;
            _rowsModel.RowPercents = _columnsModel.ColumnPercents = new int[ZoneCount];

            for (int i = 0; i < ZoneCount; i++)
            {
                _rowsModel.CellChildMap[i, 0] = i;
                _columnsModel.CellChildMap[0, i] = i;

                // Note: This is NOT equal to _multiplier / ZoneCount and is done like this to make
                // the sum of all RowPercents exactly (_multiplier).
                // _columnsModel is sharing the same array
                _rowsModel.RowPercents[i] = ((_multiplier * (i + 1)) / ZoneCount) - ((_multiplier * i) / ZoneCount);
            }

            // Update the "Grid" Default Layout
            int rows = 1;
            while (ZoneCount / rows >= rows)
            {
                rows++;
            }

            rows--;
            int cols = ZoneCount / rows;
            if (ZoneCount % rows == 0)
            {
                // even grid
            }
            else
            {
                cols++;
            }

            _gridModel.Rows = rows;
            _gridModel.Columns = cols;
            _gridModel.RowPercents = new int[rows];
            _gridModel.ColumnPercents = new int[cols];
            _gridModel.CellChildMap = new int[rows, cols];

            // Note: The following are NOT equal to _multiplier divided by rows or columns and is
            // done like this to make the sum of all RowPercents exactly (_multiplier).
            for (int row = 0; row < rows; row++)
            {
                _gridModel.RowPercents[row] = ((_multiplier * (row + 1)) / rows) - ((_multiplier * row) / rows);
            }

            for (int col = 0; col < cols; col++)
            {
                _gridModel.ColumnPercents[col] = ((_multiplier * (col + 1)) / cols) - ((_multiplier * col) / cols);
            }

            int index = 0;
            for (int col = cols - 1; col >= 0; col--)
            {
                for (int row = rows - 1; row >= 0; row--)
                {
                    _gridModel.CellChildMap[row, col] = index++;
                    if (index == ZoneCount)
                    {
                        index--;
                    }
                }
            }

            // Update the "Priority Grid" Default Layout
            if (ZoneCount <= _priorityData.Length)
            {
                _priorityGridModel.Reload(_priorityData[ZoneCount - 1]);
            }
            else
            {
                // same as grid;
                _priorityGridModel.Rows = _gridModel.Rows;
                _priorityGridModel.Columns = _gridModel.Columns;
                _priorityGridModel.RowPercents = _gridModel.RowPercents;
                _priorityGridModel.ColumnPercents = _gridModel.ColumnPercents;
                _priorityGridModel.CellChildMap = _gridModel.CellChildMap;
            }
        }

        private void ParseDeviceInfoData()
        {
            try
            {
                FileStream inputStream = File.Open(Settings.ActiveZoneSetTmpFile, FileMode.Open);
                var jsonObject = JsonDocument.Parse(inputStream, options: default).RootElement;

                UniqueKey = jsonObject.GetProperty("device-id").GetString();
                ActiveZoneSetUUid = jsonObject.GetProperty("active-zoneset").GetProperty("uuid").GetString();
                string layoutType = jsonObject.GetProperty("active-zoneset").GetProperty("type").GetString();

                if (ActiveZoneSetUUid == "null" || layoutType == "blank")
                {
                    // Default selection is Focus
                    ActiveZoneSetLayoutType = LayoutType.Focus;
                    _showSpacing = true;
                    _spacing = 16;
                    _zoneCount = 3;
                }
                else
                {
                    switch (layoutType)
                    {
                        case "focus":
                            ActiveZoneSetLayoutType = LayoutType.Focus;
                            break;
                        case "columns":
                            ActiveZoneSetLayoutType = LayoutType.Columns;
                            break;
                        case "rows":
                            ActiveZoneSetLayoutType = LayoutType.Rows;
                            break;
                        case "grid":
                            ActiveZoneSetLayoutType = LayoutType.Grid;
                            break;
                        case "priority-grid":
                            ActiveZoneSetLayoutType = LayoutType.PriorityGrid;
                            break;
                        case "custom":
                            ActiveZoneSetLayoutType = LayoutType.Custom;
                            break;
                    }

                    _showSpacing = jsonObject.GetProperty("editor-show-spacing").GetBoolean();
                    _spacing = jsonObject.GetProperty("editor-spacing").GetInt32();
                    _zoneCount = jsonObject.GetProperty("editor-zone-count").GetInt32();
                }

                inputStream.Close();
            } catch (Exception ex)
            {
                LayoutModel.ShowExceptionMessageBox("Error parsing device info data", ex);
            }
        }

        private void ParseCommandLineArgs()
        {
            _workArea = SystemParameters.WorkArea;
            Monitor = 0;

            string[] args = Environment.GetCommandLineArgs();
            if (args.Length == 7)
            {
                if (uint.TryParse(args[(int)CmdArgs.MonitorHandle], out uint monitor))
                {
                    Monitor = monitor;
                }

                var parsedLocation = args[(int)CmdArgs.X_Y_Width_Height].Split('_');
                var x = int.Parse(parsedLocation[0]);
                var y = int.Parse(parsedLocation[1]);
                var width = int.Parse(parsedLocation[2]);
                var height = int.Parse(parsedLocation[3]);

                _workArea = new Rect(x, y, width, height);

                WorkAreaKey = args[(int)CmdArgs.ResolutionKey];

                _activeZoneSetTmpFile = args[(int)CmdArgs.ActiveZoneSetTmpFile];
                _appliedZoneSetTmpFile = args[(int)CmdArgs.AppliedZoneSetTmpFile];
                _customZoneSetsTmpFile = args[(int)CmdArgs.CustomZoneSetsTmpFile];

                ParseDeviceInfoData();
            }
        }

        public IList<LayoutModel> DefaultModels { get; }

        public static ObservableCollection<LayoutModel> CustomModels
        {
            get
            {
                if (_customModels == null)
                {
                    _customModels = LayoutModel.LoadCustomModels();
                    _customModels.Insert(0, _blankCustomModel);
                }

                return _customModels;
            }
        }

        private static ObservableCollection<LayoutModel> _customModels;

        public static readonly string RegistryPath = "SOFTWARE\\SuperFancyZones";
        public static readonly string FullRegistryPath = "HKEY_CURRENT_USER\\" + RegistryPath;

        public static bool IsPredefinedLayout(LayoutModel model)
        {
            return model.Type != LayoutType.Custom;
        }

        // implementation of INotifyProeprtyChanged
        public event PropertyChangedEventHandler PropertyChanged;

        // FirePropertyChanged -- wrapper that calls INPC.PropertyChanged
        protected virtual void FirePropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
