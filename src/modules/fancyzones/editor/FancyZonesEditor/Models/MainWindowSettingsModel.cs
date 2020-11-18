// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using FancyZonesEditor.Models;

namespace FancyZonesEditor
{
    // Settings
    //  These are the configuration settings used by the rest of the editor
    //  Other UIs in the editor will subscribe to change events on the properties to stay up to date as these properties change
    public class MainWindowSettingsModel : INotifyPropertyChanged
    {
        private enum DeviceIdParts
        {
            Name = 0,
            Width,
            Height,
            VirtualDesktopId,
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
        public const ushort _lastDefinedId = _blankCustomModelId;

        private const int MaxNegativeSpacing = -10;

        // Non-localizable strings
        public static readonly string RegistryPath = "SOFTWARE\\SuperFancyZones";
        public static readonly string FullRegistryPath = "HKEY_CURRENT_USER\\" + RegistryPath;

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

        public MainWindowSettingsModel()
        {
            // Initialize the five default layout models: Focus, Columns, Rows, Grid, and PriorityGrid
            DefaultModels = new List<LayoutModel>(5);
            _focusModel = new CanvasLayoutModel(Properties.Resources.Template_Layout_Focus, LayoutType.Focus);
            DefaultModels.Add(_focusModel);

            _columnsModel = new GridLayoutModel(Properties.Resources.Template_Layout_Columns, LayoutType.Columns)
            {
                Rows = 1,
                RowPercents = new List<int>(1) { _multiplier },
            };
            DefaultModels.Add(_columnsModel);

            _rowsModel = new GridLayoutModel(Properties.Resources.Template_Layout_Rows, LayoutType.Rows)
            {
                Columns = 1,
                ColumnPercents = new List<int>(1) { _multiplier },
            };
            DefaultModels.Add(_rowsModel);

            _gridModel = new GridLayoutModel(Properties.Resources.Template_Layout_Grid, LayoutType.Grid);
            DefaultModels.Add(_gridModel);

            _priorityGridModel = new GridLayoutModel(Properties.Resources.Template_Layout_Priority_Grid, LayoutType.PriorityGrid);
            DefaultModels.Add(_priorityGridModel);

            _blankCustomModel = new CanvasLayoutModel(Properties.Resources.Custom_Layout_Create_New, LayoutType.Blank);

            UpdateTemplateLayoutModels();
        }

        // ZoneCount - number of zones selected in the picker window
        public int ZoneCount
        {
            get
            {
                return App.Overlay.CurrentLayoutSettings.ZoneCount;
            }

            set
            {
                if (App.Overlay.CurrentLayoutSettings.ZoneCount != value)
                {
                    App.Overlay.CurrentLayoutSettings.ZoneCount = value;
                    UpdateTemplateLayoutModels();
                    FirePropertyChanged(nameof(ZoneCount));
                }
            }
        }

        // Spacing - how much space in between zones of the grid do you want
        public int Spacing
        {
            get
            {
                return App.Overlay.CurrentLayoutSettings.Spacing;
            }

            set
            {
                value = Math.Max(0, value);
                if (App.Overlay.CurrentLayoutSettings.Spacing != value)
                {
                    App.Overlay.CurrentLayoutSettings.Spacing = value;
                    UpdateTemplateLayoutModels();
                    FirePropertyChanged(nameof(Spacing));
                }
            }
        }

        // ShowSpacing - is the Spacing value used or ignored?
        public bool ShowSpacing
        {
            get
            {
                return App.Overlay.CurrentLayoutSettings.ShowSpacing;
            }

            set
            {
                if (App.Overlay.CurrentLayoutSettings.ShowSpacing != value)
                {
                    App.Overlay.CurrentLayoutSettings.ShowSpacing = value;
                    UpdateTemplateLayoutModels();
                    FirePropertyChanged(nameof(ShowSpacing));
                }
            }
        }

        // SensitivityRadius - how much space inside the zone to highlight the adjacent zone too
        public int SensitivityRadius
        {
            get
            {
                return App.Overlay.CurrentLayoutSettings.SensitivityRadius;
            }

            set
            {
                value = Math.Max(0, value);
                if (App.Overlay.CurrentLayoutSettings.SensitivityRadius != value)
                {
                    App.Overlay.CurrentLayoutSettings.SensitivityRadius = value;
                    UpdateTemplateLayoutModels();
                    FirePropertyChanged(nameof(SensitivityRadius));
                }
            }
        }

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
                    FirePropertyChanged(nameof(IsShiftKeyPressed));
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
                    FirePropertyChanged(nameof(IsCtrlKeyPressed));
                }
            }
        }

        private bool _isCtrlKeyPressed;

        // UpdateLayoutModels
        // Update the five default layouts based on the new ZoneCount
        private void UpdateTemplateLayoutModels()
        {
            // Update the "Focus" Default Layout
            _focusModel.Zones.Clear();

            // Sanity check for imported settings that may have invalid data
            if (ZoneCount < 1)
            {
                ZoneCount = 3;
            }

            // If changing focus layout zones size and/or increment,
            // same change should be applied in ZoneSet.cpp (ZoneSet::CalculateFocusLayout)
            var workingArea = App.Overlay.WorkArea;
            int topLeftCoordinate = (int)App.Overlay.ScaleCoordinateWithCurrentMonitorDpi(100); // TODO: replace magic numbers
            int width = (int)(workingArea.Width * 0.4);
            int height = (int)(workingArea.Height * 0.4);
            Int32Rect focusZoneRect = new Int32Rect(topLeftCoordinate, topLeftCoordinate, width, height);
            int focusRectXIncrement = (ZoneCount <= 1) ? 0 : (int)App.Overlay.ScaleCoordinateWithCurrentMonitorDpi(50);
            int focusRectYIncrement = (ZoneCount <= 1) ? 0 : (int)App.Overlay.ScaleCoordinateWithCurrentMonitorDpi(50);

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
            _rowsModel.RowPercents = _columnsModel.ColumnPercents = new List<int>(ZoneCount);

            for (int i = 0; i < ZoneCount; i++)
            {
                _rowsModel.CellChildMap[i, 0] = i;
                _columnsModel.CellChildMap[0, i] = i;

                // Note: This is NOT equal to _multiplier / ZoneCount and is done like this to make
                // the sum of all RowPercents exactly (_multiplier).
                // _columnsModel is sharing the same array
                _rowsModel.RowPercents.Add(((_multiplier * (i + 1)) / ZoneCount) - ((_multiplier * i) / ZoneCount));
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
            _gridModel.RowPercents = new List<int>(rows);
            _gridModel.ColumnPercents = new List<int>(cols);
            _gridModel.CellChildMap = new int[rows, cols];

            // Note: The following are NOT equal to _multiplier divided by rows or columns and is
            // done like this to make the sum of all RowPercents exactly (_multiplier).
            for (int row = 0; row < rows; row++)
            {
                _gridModel.RowPercents.Add(((_multiplier * (row + 1)) / rows) - ((_multiplier * row) / rows));
            }

            for (int col = 0; col < cols; col++)
            {
                _gridModel.ColumnPercents.Add(((_multiplier * (col + 1)) / cols) - ((_multiplier * col) / cols));
            }

            int index = 0;
            for (int row = 0; row < rows; row++)
            {
                for (int col = 0; col < cols; col++)
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

        public CanvasLayoutModel BlankModel
        {
            get
            {
                return _blankModel;
            }
        }

        private CanvasLayoutModel _blankModel = new CanvasLayoutModel(string.Empty, LayoutType.Blank);

        public static bool IsPredefinedLayout(LayoutModel model)
        {
            return model.Type != LayoutType.Custom;
        }

        public LayoutModel UpdateSelectedLayoutModel()
        {
            UpdateTemplateLayoutModels();
            ResetAppliedModel();
            ResetSelectedModel();

            LayoutModel foundModel = null;
            LayoutSettings currentApplied = App.Overlay.CurrentLayoutSettings;

            // set new layout
            if (currentApplied.Type == LayoutType.Blank)
            {
                foundModel = BlankModel;
            }
            else if (currentApplied.Type == LayoutType.Custom)
            {
                foreach (LayoutModel model in MainWindowSettingsModel.CustomModels)
                {
                    if ("{" + model.Guid.ToString().ToUpper() + "}" == currentApplied.ZonesetUuid.ToUpper())
                    {
                        // found match
                        foundModel = model;
                        break;
                    }
                }
            }
            else
            {
                foreach (LayoutModel model in DefaultModels)
                {
                    if (model.Type == currentApplied.Type)
                    {
                        // found match
                        foundModel = model;
                        break;
                    }
                }
            }

            if (foundModel == null)
            {
                foundModel = DefaultModels[4]; // PriorityGrid
            }

            foundModel.IsSelected = true;
            foundModel.IsApplied = true;

            FirePropertyChanged(nameof(IsCustomLayoutActive));
            return foundModel;
        }

        public void ResetSelectedModel()
        {
            foreach (LayoutModel model in CustomModels)
            {
                if (model.IsSelected)
                {
                    model.IsSelected = false;
                    break;
                }
            }

            foreach (LayoutModel model in DefaultModels)
            {
                if (model.IsSelected)
                {
                    model.IsSelected = false;
                    break;
                }
            }
        }

        public void ResetAppliedModel()
        {
            foreach (LayoutModel model in CustomModels)
            {
                if (model.IsApplied)
                {
                    model.IsApplied = false;
                    break;
                }
            }

            foreach (LayoutModel model in DefaultModels)
            {
                if (model.IsApplied)
                {
                    model.IsApplied = false;
                    break;
                }
            }
        }

        public void UpdateDesktopDependantProperties(LayoutSettings prevSettings)
        {
            UpdateTemplateLayoutModels();

            if (prevSettings.ZoneCount != ZoneCount)
            {
                FirePropertyChanged(nameof(ZoneCount));
            }

            if (prevSettings.Spacing != Spacing)
            {
                FirePropertyChanged(nameof(Spacing));
            }

            if (prevSettings.ShowSpacing != ShowSpacing)
            {
                FirePropertyChanged(nameof(ShowSpacing));
            }

            if (prevSettings.SensitivityRadius != SensitivityRadius)
            {
                FirePropertyChanged(nameof(SensitivityRadius));
            }
        }

        // implementation of INotifyPropertyChanged
        public event PropertyChangedEventHandler PropertyChanged;

        // FirePropertyChanged -- wrapper that calls INPC.PropertyChanged
        protected virtual void FirePropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
